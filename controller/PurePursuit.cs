using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;


namespace multiagent.controller
{
    public class PurePursuit
    {
        [SerializeField] private double lookAheadDis = 0.6;
        [SerializeField] private Vector2 Kp;
        [SerializeField] private Vector2 Ki;
        [SerializeField] private Vector2 Kd;
        public Vector2 error;
        public Vector2 ierror; // Integral Error
        public Vector2 derror; // Derivative Error
        public Vector2 ctrl;
        public Vector2 minLim;
        public Vector2 maxLim;
        private int lastFoundIndex = 0;

        

        public void initParameter(float Kp_lin = 0, float Kp_turn = 0,
                                float Ki_lin = 0, float Ki_turn = 0,
                                float Kd_lin = 0, float Kd_turn = 0,
                                float lookAheadDis = 0, Vector2 minLim = default, Vector2 maxLim = default)
        {
            this.Kp = new Vector2(Kp_lin, Kp_turn);
            this.Ki = new Vector2(Ki_lin, Ki_turn);
            this.Kd = new Vector2(Kd_lin, Kd_turn);
            this.lookAheadDis = lookAheadDis;
            this.minLim = minLim;
            this.maxLim = maxLim;

        }


        public Vector2 getCtrl(Vector3 state, Vector3 desState, Vector3 dstate = default, Vector3 ddesState = default,bool useDerivative = false)
        {
            Vector3 diffState = desState -state;
            float distError = MathF.Sqrt((float)(Math.Pow(diffState.x,2) + Math.Pow(diffState.y,2)));
            float turnError = find_angle_diff(diffState.z * 180 / MathF.PI, 0);

            error = new Vector2(distError,turnError);
            if (MathF.Abs(Kp.x) > 0 && MathF.Abs(Kp.y) > 0)
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
            else
            {
                if (dstate != default && ddesState != default)
                {
                    error.x = ddesState.x / Kp.x;
                }
            }
            if (MathF.Abs(Ki.x) > 0 && MathF.Abs(Ki.y) > 0)
            {
                ierror += error * Time.deltaTime;
                ierror[0] = Math.Clamp(ierror[0], minLim[0] / Ki[0], maxLim[0] / Ki[0]);
                ierror[1] = Math.Clamp(ierror[1], minLim[1] / Ki[1], maxLim[1] / Ki[1]);
            }
            
            ctrl = Kp*error + Ki*ierror + Kd*derror;
            return ctrl;
        }

        public (Vector3, int) goal_pt_search(Vector3[] path, Vector3 state, int lastFoundIndex = -1, Vector2 refVector = default)
        {
            Vector2 goalPt;
            Vector2 [] path2d = System.Array.ConvertAll<Vector3, Vector2> (path, Util.getV3fromV2);
            Vector3 desState;
            int startingIndex;
            Vector2 currentPos = new Vector2(state.x, state.y);
            if (refVector == default)
            {
                refVector = Vector2.right;
            }
            if (lastFoundIndex == -1)
            {
                goalPt = path2d[this.lastFoundIndex];
                startingIndex = this.lastFoundIndex;
            }
            else
            {
                goalPt = path2d[lastFoundIndex];
                startingIndex = lastFoundIndex;
            }
            bool intersectFound = false;
            for (int i = startingIndex; i < path2d.Length - 1; i++)
            {

                (intersectFound, goalPt) = line_circle_intersection(currentPos, path2d[i], path2d[i + 1]);
                if (intersectFound)
                {
                    if ((goalPt - path2d[i + 1]).magnitude < (currentPos - path2d[i + 1]).magnitude)
                    {
                        this.lastFoundIndex = i;
                        break;
                    }
                    else
                    {
                        this.lastFoundIndex = i + 1;
                    }

                }
                else
                {
                    goalPt = path2d[this.lastFoundIndex];
                }

            }
            (float angle,bool equalRefAngle) = Util.perform_angle(goalPt, currentPos, refVector, path[this.lastFoundIndex].z);
            if (equalRefAngle)
            {
                desState = new Vector3(currentPos.x,currentPos.y,angle);
            }
            else
            {
                desState = new Vector3(goalPt.x,goalPt.y,angle);
            }
            return (desState, lastFoundIndex);
        }

        public (bool, Vector2) line_circle_intersection(Vector2 currentPos, Vector2 pt1, Vector2 pt2)
        {
            Vector2 pt1_offset = pt2 - currentPos;
            Vector2 pt2_offset = pt1 - currentPos;
            Vector2 pt21 = pt2_offset - pt1_offset;
            float dr = pt21.magnitude;
            float D = pt1_offset.x * pt2_offset.y - pt1_offset.y * pt2_offset.x;
            float discriminant = (float)(MathF.Pow((float)(lookAheadDis * dr), 2) - Math.Pow(D, 2));

            bool intersectFound = false;
            Vector2 sol = Vector2.zero;
            if (discriminant >= 0)
            {
                float ax = D * pt21.y;
                float ay = D * pt21.x;
                float bx = MathF.Sign(pt21[1]) * pt21[0] * MathF.Sqrt(discriminant);
                float by = MathF.Abs(pt21[1]) * MathF.Sqrt(discriminant);
                float dem = MathF.Pow(dr, 2);

                float minX = Mathf.Min(pt1[0], pt2[0]);
                float maxX = Mathf.Max(pt1[0], pt2[0]);
                float minY = Mathf.Min(pt1[1], pt2[1]);
                float maxY = Mathf.Max(pt1[1], pt2[1]);

                Vector2 solpt1 = currentPos + new Vector2((ax + bx) / dem, (-ay + by) / dem);
                Vector2 solpt2 = currentPos + new Vector2((ax - bx) / dem, (-ay - by) / dem);

                bool solpt1Yes = (minX <= solpt1.x) && (solpt1.x <= maxX) && (minY <= solpt1.y) && (solpt1.y <= maxY);
                bool solpt2Yes = (minX <= solpt2.x) && (solpt2.x <= maxX) && (minY <= solpt2.y) && (solpt2.y <= maxY);
                if (solpt1Yes && solpt2Yes)
                {
                    intersectFound = true;
                    sol = ((pt2 - solpt1).magnitude > (pt2 - solpt2).magnitude) ? solpt1 : solpt2;
                }
                else if (solpt1Yes)
                {
                    intersectFound = true;
                    sol = solpt1;
                }
                else if (solpt2Yes)
                {
                    intersectFound = true;
                    sol = solpt2;
                }
                else
                {
                    intersectFound = false;
                    sol = Vector2.zero;
                }
            }

            return (intersectFound, sol);

        }

        public float find_angle_diff(float absTargetAngle, float currentHeading)
        {
            float angleDiff = absTargetAngle - currentHeading;
            if (angleDiff > 180 || angleDiff < -180)
            {
                angleDiff = -1 * MathF.Sign(angleDiff) * (360 - MathF.Abs(angleDiff));       
            }
            return angleDiff*MathF.PI/180;
        }
    }
}