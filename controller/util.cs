using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;


namespace multiagent.controller
{
    public static class Util
    {
        public static float find_angle_diff(float absTargetAngle, float currentHeading)
        {
            float angleDiff = absTargetAngle - currentHeading;
            if (angleDiff > 180 || angleDiff < -180)
            {
                angleDiff = -1 * MathF.Sign(angleDiff) * (360 - MathF.Abs(angleDiff));
            }
            return angleDiff * MathF.PI / 180;
        }

        public static (float,bool) perform_angle(Vector2 goalPt, Vector2 currentPos, Vector2 refVector, float refAngle = 0)
        {
            Vector2 diff = new Vector2(goalPt.x - currentPos.x, goalPt.y - currentPos.y);
            float angle;
            if (diff.magnitude > 0.1f)
            {
                angle = Vector2.SignedAngle(diff, refVector);
                angle = angle < 0 ? angle + 360 : angle;
                angle = find_angle_diff(angle, 0);
            }
            else
            {
                angle = refAngle;
            }
            return (angle,angle==refAngle && diff.magnitude <= 0.1f);
        }

        public static Vector2 getV3fromV2 (Vector3 v3)
        {
            return new Vector2 (v3.x, v3.y);
        }
    }
}