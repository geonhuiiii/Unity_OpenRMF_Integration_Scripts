using UnityEngine;

namespace multiagent
{
    public class GUI_Robot : MonoBehaviour
    {
        [SerializeField] private Robot _robot;

        private GUIStyle _defaultStyle = new GUIStyle();
        private GUIStyle _positivieStyle = new GUIStyle();
        private GUIStyle _negativeStyle = new GUIStyle();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _defaultStyle.fontSize = 50;
            _defaultStyle.normal.textColor = Color.yellow;

            _positivieStyle.fontSize = 50;
            _positivieStyle.normal.textColor = Color.green;

            _negativeStyle.fontSize = 50;
            _negativeStyle.normal.textColor = Color.red;

        }

        private void OnGUI()
        {
            string debugEpisode = "Episode: " + _robot.CurrentEpisode + " - Step: " + _robot.StepCount;
            string debugReward = "Reward: " + _robot.CumulativeReward.ToString();

            GUIStyle rewardStyle = _robot.CumulativeReward < 0 ? _negativeStyle : _positivieStyle;


            GUI.Label(new Rect(20, 20, 500, 30), debugEpisode, _defaultStyle);
            GUI.Label(new Rect(20, 60 , 500, 30), debugReward, rewardStyle);
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}