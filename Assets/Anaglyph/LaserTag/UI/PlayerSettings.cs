using Anaglyph.Lasertag.Networking;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Anaglyph.Lasertag
{
    public class PlayerSettings : MonoBehaviour
    {
        [SerializeField]
        private Text teamNumberText;

        private void Update()
        {
            teamNumberText.text = $"{MainPlayer.Instance.currentRole.TeamNumber}";
        }

        public void SetTeam(byte team)
        {
            MainPlayer.Instance.networkPlayer.TeamOwner.teamSync.Value = team;
        }

        public void IncrementTeamNumber()
        {
            MainPlayer.Instance.currentRole.TeamNumber++;
        }

        public void DecrementTeamNumber()
        {
            MainPlayer.Instance.currentRole.TeamNumber--;
        }

        public void SetBaseAffinity(bool shouldReturn)
        {
            MainPlayer.Instance.currentRole.ReturnToBaseOnDie = shouldReturn;
        }
    }
}
