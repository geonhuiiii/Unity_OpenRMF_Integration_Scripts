using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;


namespace multiagent.controller
{
    public class PID
    {
        [SerializeField] public Vector2 Kp;
        [SerializeField] public Vector2 Ki;
        [SerializeField] public Vector2 Kd;
        public Vector2 error;
        public Vector2 ierror; // Integral Error
        public Vector2 derror; // Derivative Error
        public Vector2 ctrl;
        public Vector2 minLim;
        public Vector2 maxLim;
        // private int lastFoundIndex = 0;

        public void initParameter(float Kp_lin = 0, float Kp_turn = 0,
                                float Ki_lin = 0, float Ki_turn = 0,
                                float Kd_lin = 0, float Kd_turn = 0,
                                Vector2 minLim = default, Vector2 maxLim = default)
        {
            this.Kp = new Vector2(Kp_lin, Kp_turn);
            this.Ki = new Vector2(Ki_lin, Ki_turn);
            this.Kd = new Vector2(Kd_lin, Kd_turn);
            this.minLim = minLim;
            this.maxLim = maxLim;

        }


        public Vector2 getCtrl(Vector3 state, Vector3 desState, Vector3 dstate = default, Vector3 ddesState = default,bool useDerivative = false)
        {
            Vector3 diffState = desState - state;
            float distError = MathF.Sqrt((float)(Math.Pow(diffState.x, 2) + Math.Pow(diffState.y, 2)));
            float turnError = Util.find_angle_diff(diffState.z * 180 / MathF.PI, 0);
            

            error = new Vector2(distError, turnError);
            if (MathF.Abs(Kp.x) > 0 && MathF.Abs(Kp.y) > 0)
            {
                if (useDerivative)
                {
                    float ddistError = ((Vector2)(ddesState-dstate)).magnitude;
                    float dturnError = ddesState.z - dstate.z;
                    derror = new Vector2(ddistError, dturnError);
                }
                else
                {
                    derror = error / Time.deltaTime;
                }
            }
            else
            {
                if (dstate != default && ddesState != default)
                {
                    error.x = ddesState.x / Kp.x;
                }
            }
            if (MathF.Abs(Ki.x) > 0 || MathF.Abs(Ki.y) > 0)
            {
                ierror += error * Time.deltaTime;
                if (MathF.Abs(Ki.x) > 0)
                {
                    ierror[0] = Math.Clamp(ierror[0], minLim[0] / Ki[0], maxLim[0] / Ki[0]);
                }
                if (MathF.Abs(Ki.y) > 0)
                {
                    ierror[1] = Math.Clamp(ierror[1], minLim[1] / Ki[1], maxLim[1] / Ki[1]);
                }
                
            }

            ctrl = Kp * error + Ki * ierror + Kd * derror;
            Debug.Log($"P: {Kp * error} | I: {Ki * ierror} | D: {Kd * derror}");
            return ctrl;
        }
    }
}