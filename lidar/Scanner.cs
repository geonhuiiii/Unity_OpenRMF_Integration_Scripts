//Author: Angel Ortiz
//Date: 08/15/17

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace multiagent.lidar
{
    public class Scanner : MonoBehaviour
    {
        public LaserLine laserLinePrefab;

        [Range(.1f, 50f)] public float rotationFrequency;
        [SerializeField] float scanArea;
        [SerializeField] float scansPerSecond;
        [Range(1, 360)] public int scanAreaPerSteps;
        private int rotationPerSteps;
        public ScannerData lidarDataDict;

        //Data obtained from HDL-64E S3 Velodine Lidar spec sheet.
        const float verticalAngularRes = 0.4f;
        [SerializeField] int laserChannels = 64;
        const float verticalStartPoint = 0f;
        Transform parentTransform;
        LaserLine[] laserArray;
        Vector3[] laserImpactLocs;
        Vector3 rotation;
        Quaternion initRotation;

        [SerializeField] bool debug = false;

        //Initializing object & lidar scan FOV and pre-calculating scans/s.
        void Awake()
        {
            parentTransform = transform.parent.transform.GetComponent<Transform>();
            initRotation = transform.localRotation;
            createLidarScan();
            scansPerSecond = calculateScansPerSecond();
        }
        //Every physics update the scanner will rotate, then query all the laserbeams and store results in
        //the lidar data structure. 
        void FixedUpdate()
        {
            for (int i = 0; i < scanAreaPerSteps; i++)
            {
                int currentAngle = (int)transform.localRotation.eulerAngles.y;
                if (currentAngle % rotationPerSteps == rotationPerSteps - 1) currentAngle++; //Fixing floating point rounding issue.
                currentAngle %= 360; //Ensuring angle is between 0 and 360 degrees.
                updateLaserImpactLocations();
                lidarDataDict.addPointsAtAngle(currentAngle, laserImpactLocs);
                transform.Rotate(rotation);
            }
        }

        //Instantiate lidar Scanner and calculates the scanArea.
        //Also intantiates the data structure holding the lidar data.
        void createLidarScan()
        {
            laserArray = new LaserLine[laserChannels];
            laserImpactLocs = new Vector3[laserChannels];
            for (int i = 0; i < laserChannels; i++)
            {
                // Vector3 tiltAngle = new Vector3(verticalStartPoint + i * verticalAngularRes,transform.rotation.y,0);
                Vector3 tiltAngle = new Vector3(verticalStartPoint + i * verticalAngularRes, 0, 0);
                laserArray[i] = spawnLaserBeam(tiltAngle);
            }

            scanArea = MathF.Round(360 * rotationFrequency * Time.fixedDeltaTime * 100) / 100; //Fixed rotation speed to match physics updates
            int numOfPoints = (int)(laserChannels * scanAreaPerSteps * (360 / scanArea)); //Number of points rendered at any given time ste[
            lidarDataDict = new ScannerData(numOfPoints);
            rotationPerSteps = (int)(scanArea / scanAreaPerSteps);
            rotation = new Vector3(0, rotationPerSteps, 0);
        }

        //Queries each laser for their impact location and stores values
        void updateLaserImpactLocations()
        {
            Quaternion shiftedRotation = Quaternion.Inverse(parentTransform.localRotation) * initRotation;
            for (int i = 0; i < laserArray.Length; i++)
            {
                laserImpactLocs[i] = shiftedRotation * laserArray[i].getRay(debug);
            }
        }

        //Returns scans per second based on physics deltaTime.
        float calculateScansPerSecond()
        {
            return laserChannels * 1 / Time.fixedDeltaTime;
        }

        //Spawning laserLine objects and assigning its position, rotation and parent transform.
        LaserLine spawnLaserBeam(Vector3 tiltAngle)
        {
            LaserLine spawn = Instantiate<LaserLine>(laserLinePrefab);
            spawn.transform.localPosition = transform.position;
            spawn.transform.localRotation = Quaternion.Euler(tiltAngle);
            spawn.transform.parent = gameObject.transform;

            return spawn;
        }


    }
}