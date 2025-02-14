using Anaglyph.Lasertag.Networking;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Anaglyph.Lasertag
{
	public enum WinCondition : byte
	{
		None = 0,
		Timer = 1,
		ReachScore = 2,
	}

	public enum RoundState : byte
	{
		NotPlaying = 0,
		Queued = 1,
		Countdown = 2,
		Playing = 3,
	}

	[Serializable]
	public struct RoundSettings : INetworkSerializeByMemcpy
	{
		public bool teams;
		public bool respawnInBases;

		public byte pointsPerKill;
		public byte pointsPerSecondHoldingPoint;

		public WinCondition winCondition;
		public int timerSeconds;
		public short scoreTarget;

		public bool CheckWinByTimer() => winCondition.HasFlag(WinCondition.Timer);
		public bool CheckWinByPoints() => winCondition.HasFlag(WinCondition.ReachScore);
	}

	public class RoundManager : NetworkBehaviour
	{
		private const string NotOwnerExceptionMessage = "Only the NGO owner should call this!";

		public static RoundManager Instance { get; private set; }

		private NetworkVariable<RoundState> roundStateSync = new(RoundState.NotPlaying);
		public static RoundState RoundState => Instance.roundStateSync.Value;

		private NetworkVariable<float> timeRoundEndsSync = new(0);
		public static float TimeRoundEnds => Instance.timeRoundEndsSync.Value;

		private NetworkVariable<int> team0ScoreSync = new(0);
		private NetworkVariable<int> team1ScoreSync = new(0);
		private NetworkVariable<int> team2ScoreSync = new(0);

		private NetworkVariable<int>[] teamScoresSync;
		private NetworkVariable<byte> winningTeamSync = new();
		public static int GetTeamScore(byte team) => Instance.teamScoresSync[team].Value;
		public static byte WinningTeam => Instance.winningTeamSync.Value;

		private NetworkVariable<RoundSettings> activeSettingsSync = new();
		public static RoundSettings ActiveSettings => Instance.activeSettingsSync.Value;

		public static event Action<RoundState, RoundState> OnRoundStateChange = delegate { };
		public static event Action OnNotPlaying = delegate { };
		public static event Action OnQueued = delegate { };
		public static event Action OnCountdown = delegate { };
		public static event Action OnPlaying = delegate { };
		public static event Action OnPlayEnd = delegate { };
		public static event Action OnBecomeMaster = delegate { };
		public static event Action OnLoserMaster = delegate { };

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void OnApplicationInit()
		{
			OnRoundStateChange = delegate { };
			OnNotPlaying = delegate { };
			OnQueued = delegate { };
			OnCountdown = delegate { };
			OnPlaying = delegate { };
			OnPlayEnd = delegate { };
			OnBecomeMaster = delegate { };
			OnLoserMaster = delegate { };
		}

		private void OwnerCheck()
		{
			if(!IsOwner) throw new Exception(NotOwnerExceptionMessage);
		}

		private void Awake()
		{
			Instance = this;

			teamScoresSync = new NetworkVariable<int>[TeamManagement.NumTeams];

			teamScoresSync[0] = team0ScoreSync;
			teamScoresSync[1] = team1ScoreSync;
			teamScoresSync[2] = team2ScoreSync;

			roundStateSync.OnValueChanged += OnStateUpdateLocally;

			OnStateUpdateLocally(RoundState.NotPlaying, RoundState.NotPlaying);
		}

		private void Update()
		{
			if (!IsSpawned) return;

			Networking.Avatar mainNetworkPlayer = MainPlayer.Instance.networkPlayer;

			if (RoundState == RoundState.NotPlaying || RoundState == RoundState.Queued || mainNetworkPlayer.Team == 0) {
				
				if (mainNetworkPlayer.IsInBase)
					mainNetworkPlayer.TeamOwner.teamSync.Value = mainNetworkPlayer.InBase.Team;
			}
		}

		private void OnStateUpdateLocally(RoundState prev, RoundState state)
		{
			OnRoundStateChange.Invoke(prev, state);

			switch (state)
			{
				case RoundState.NotPlaying:
					OnNotPlaying();
					if (prev == RoundState.Playing) OnPlayEnd();

					MainPlayer.Instance.Respawn();
					MainPlayer.Instance.currentRole.ReturnToBaseOnDie = false;

					break;

				case RoundState.Queued:

					OnQueued();

					break;

				case RoundState.Countdown:

					OnCountdown();
					break;

				case RoundState.Playing:
					OnPlaying();

					MainPlayer.Instance.Respawn();
					MainPlayer.Instance.currentRole.ReturnToBaseOnDie = ActiveSettings.respawnInBases;

					break;
			}
		}

		public override void OnGainedOwnership()
		{
			OnBecomeMaster.Invoke();

			if(RoundState == RoundState.Playing)
				SubscribeToEvents();
		}

		public override void OnLostOwnership()
		{
			OnLoserMaster.Invoke();
			UnsubscribeFromEvents();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			UnsubscribeFromEvents();
		}

		public override void OnNetworkDespawn()
		{
			UnsubscribeFromEvents();
		}

		[Rpc(SendTo.Owner)]
		public void QueueStartGameOwnerRpc(RoundSettings gameSettings)
		{
			activeSettingsSync.Value = gameSettings;
			roundStateSync.Value = RoundState.Queued;
			StartCoroutine(QueueStartGameAsOwnerCoroutine());
		}

		private IEnumerator QueueStartGameAsOwnerCoroutine()
		{
			OwnerCheck();

			ResetScoresRpc();

			yield return new WaitForSeconds(1);

			while (RoundState == RoundState.Queued)
			{
				if (ActiveSettings.respawnInBases)
				{
					int numPlayersInbase = 0;

					foreach (Networking.Avatar player in Networking.Avatar.AllPlayers.Values)
					{
						if (player.IsInBase)
							numPlayersInbase++;
					}

					if (numPlayersInbase == Networking.Avatar.AllPlayers.Count)
						roundStateSync.Value = RoundState.Countdown;

				} else
					roundStateSync.Value = RoundState.Countdown;

				yield return null;
			}

			if(RoundState == RoundState.Countdown)
				yield return new WaitForSeconds(3);

			if(RoundState == RoundState.Countdown)
				StartGameOwnerRpc();
		}

		[Rpc(SendTo.Owner)]
		public void ResetScoresRpc()
		{
			for (byte i = 0; i < teamScoresSync.Length; i++)
			{
				teamScoresSync[i].Value = 0;
			}

			foreach(Networking.Avatar player in Networking.Avatar.AllPlayers.Values)
			{
				player.ResetScoreRpc();
			}

			foreach (ControlPoint point in ControlPoint.AllControlPoints)
			{
				point.ResetPointRpc();
			}

			winningTeamSync.Value = 0;
		}

		[Rpc(SendTo.Owner)]
		private void StartGameOwnerRpc()
		{
			ResetScoresRpc();

			if (ActiveSettings.CheckWinByTimer())
				timeRoundEndsSync.Value = (float)NetworkManager.LocalTime.Time + ActiveSettings.timerSeconds;

			roundStateSync.Value = RoundState.Playing;
			SubscribeToEvents();
		}

		private void SubscribeToEvents()
		{
			OwnerCheck();

			if (ActiveSettings.CheckWinByTimer())
				StartCoroutine(GameTimerAsOwnerCoroutine());

			// sub to score events
			Networking.Avatar.OnPlayerKilledPlayer += OnPlayerKilledPlayer;
			StartCoroutine(ControlPointLoopCoroutine());
		}

		private IEnumerator GameTimerAsOwnerCoroutine()
		{
			OwnerCheck();

			while (RoundState == RoundState.Playing)
			{
				if(NetworkManager.LocalTime.TimeAsFloat > TimeRoundEnds)
					EndGameOwnerRpc();

				yield return null;
			}

			//yield return new WaitForSeconds(ActiveSettings.timerSeconds);

			EndGameOwnerRpc();
		}

		private void OnPlayerKilledPlayer(Networking.Avatar killer, Networking.Avatar victim)
		{
			OwnerCheck();

			if (ActiveSettings.teams)
			{
				ScoreTeamRpc(killer.Team, ActiveSettings.pointsPerKill);
			} else
			{
				
			}
		}

		private IEnumerator ControlPointLoopCoroutine()
		{
			OwnerCheck();

			while (RoundState == RoundState.Playing)
			{
				foreach (ControlPoint point in ControlPoint.AllControlPoints)
				{
					if (point.MillisCaptured == 0 && point.HoldingTeam != 0)
					{
						ScoreTeamRpc(point.HoldingTeam, ActiveSettings.pointsPerSecondHoldingPoint);
					}
				}

				yield return new WaitForSeconds(1);
			}
		}

		[Rpc(SendTo.Owner)]
		public void ScoreTeamRpc(byte team, int points)
		{
			if (team == 0) return;

			teamScoresSync[team].Value += points;

			byte winningTeam = 0;
			int highScore = 0;
			for(byte i = 0; i < teamScoresSync.Length; i++)
			{
				int score = GetTeamScore(i);
				if (score > highScore) {
					highScore = score;
					winningTeam = i;
				}
			}
			winningTeamSync.Value = winningTeam;

			if (ActiveSettings.CheckWinByPoints() && teamScoresSync[team].Value > ActiveSettings.scoreTarget)
			{
				EndGameOwnerRpc();
			}
		}

		[Rpc(SendTo.Owner)]
		public void EndGameOwnerRpc()
		{
			roundStateSync.Value = RoundState.NotPlaying;
			UnsubscribeFromEvents();
		}

		private void UnsubscribeFromEvents()
		{
			Networking.Avatar.OnPlayerKilledPlayer -= OnPlayerKilledPlayer;
		}


	}
}
