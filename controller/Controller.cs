using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;


namespace multiagent.controller
{

    public enum ctrlOption
    {
        PID,
        PurePursuit,
        None,
    }
    public class Controller
    {
        public PID pidController = new PID();
        public PurePursuit purePursuitController = new PurePursuit();
        public Dictionary<string, float> parameters = new Dictionary<string, float>();
        private string ctrlName = "";
        private int ctrlLength = 0;
        private void getParameters()
        {
            string path = Path.Combine("Assets", "Scripts", "controller", "control.txt");
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                for (var i = 0; i < lines.Length; i += 1)
                {
                    var line = lines[i];
                    if (line == null)
                    {
                        return;
                    }

                    if (line == String.Empty)
                    {
                        continue;
                    }

                    string[] words = line.Split(" ");

                    if (words.Length != 2)
                    {
                        continue;
                    }
                    string key = words[0];
                    float val = (float)Convert.ToDouble(words[1]);
                    parameters[key] = val;
                }
            }
            else
            {
                Debug.LogError("Text file not found at: " + path);
            }
        }

        public void InitControl(int ctrlLength, float[] minLim, float[] maxLim, string ctrlName = "")
        {
            this.ctrlName = ctrlName;
            this.ctrlLength = ctrlLength;
            getParameters();
            switch (ctrlName)
            {
                case "PID":
                    pidController.initParameter(
                        parameters["kp_lin"], parameters["kp_turn"],
                        parameters["ki_lin"], parameters["ki_turn"],
                        parameters["kd_lin"], parameters["kd_turn"],
                        new Vector2(minLim[0], minLim[1]), new Vector2(maxLim[0], maxLim[1]));
                    break;
                case "PurePursuit":
                    purePursuitController.initParameter(
                        parameters["kp_lin"], parameters["kp_turn"],
                        parameters["ki_lin"], parameters["ki_turn"],
                        parameters["kd_lin"], parameters["kd_turn"],
                        parameters["lookAheadDis"], new Vector2(minLim[0], minLim[1]), new Vector2(maxLim[0], maxLim[1]));
                    break;
            }
        }
        public Vector2 GetControl(Vector3[] desState, Vector3[] state = null, Vector3[] ddesState = null, Vector3[] dstate = null)
        {
            Vector2 action = new Vector2(desState[desState.Length - 1].x, desState[desState.Length - 1].y);
            Vector3 S, desS, dS, ddesS;
            switch (ctrlName)
            {
                case "PID":
                    S = state[0];
                    (float angle, bool equalRefAngle) = Util.perform_angle(action, new Vector2(S.x, S.y), Vector2.right, desState[desState.Length - 1].z);
                    Debug.Log($"action {action} | equalRefAngle {equalRefAngle}");
                    if (equalRefAngle)
                    {
                        desS = new Vector3(S.x, S.y, angle); //The angle is fixed in the getCtrl
                        pidController.Kd.y = 10f;
                    }
                    else
                    {
                        desS = new Vector3(action[0], action[1], angle); //The angle is fixed in the getCtrl
                        pidController.Kd.y = .1f;
                    }

                    if (dstate != null && ddesState != null)
                    {
                        dS = dstate[0];
                        ddesS = ddesState[0];
                        action = pidController.getCtrl(S, desS, dS, ddesS, true);
                    }
                    else
                    {
                        action = pidController.getCtrl(S, desS);

                    }
                    Debug.Log($"Angle: {angle} | State {S} | desS {desS} | action {action}");
                    break;
                case "PurePursuit":
                    Assert.Greater(desState.Length, 1);
                    (Vector3 foundS, int lastFoundIndex) = purePursuitController.goal_pt_search(desState, state[0]);
                    desS = foundS;
                    S = state[0];
                    if (dstate != null && ddesState != null)
                    {
                        dS = dstate[0];
                        ddesS = ddesState[0];
                        action = purePursuitController.getCtrl(S, desS, dS, ddesS, true);
                    }
                    else
                    {
                        action = purePursuitController.getCtrl(S, desS);

                    }
                    break;
                case "": // No Controller option (pass the desired accleration or velocity input)
                    break;
            }
            // action = Round(action,2);
            return action;
        }
        
        public Vector2 Round(Vector2 vector2, int decimalPlaces = 2)
        {
            float multiplier = 1;
            for (int i = 0; i < decimalPlaces; i++)
            {
                multiplier *= 10f;
            }
            return new Vector2(
                Mathf.Round(vector2.x * multiplier) / multiplier,
                Mathf.Round(vector2.y * multiplier) / multiplier);
        }
    }
}
