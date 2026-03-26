//Author: Angel Ortiz
//Date: 08/15/17

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace multiagent.lidar
{
    public class LaserLine : MonoBehaviour
    {

        const float laserMaxLength = 25;
        Vector3 endPosition;

        //Returns a raycasthit point in world coordinate if the ray "laser" encounters a physics collider, (0,0,0) if it doesn't.
        public Vector3 getRay(bool debug = false)
        {

            Ray ray = new Ray(transform.position, Quaternion.Inverse(transform.localRotation) * transform.right);
            RaycastHit raycastHit;
            if (Physics.Raycast(ray, out raycastHit, laserMaxLength))
            {
                endPosition = raycastHit.point - transform.position;
                if (debug)
                {
                    Debug.DrawLine(transform.position, raycastHit.point, Color.red);
                }

            }
            else
            {
                endPosition = Vector3.zero;
            }
            return endPosition;
        }
    }
}