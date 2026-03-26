//Author: Angel Ortiz
//Date: 08/15/17

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace multiagent.lidar
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PointCloud : MonoBehaviour
    {
        private Mesh mesh;
        private ScannerData lidarData;
        private int numPoints;
        private int[] indecies;
        private Color[] colors;
        private Vector3[] points;
        [SerializeField] private bool debug = false; //Debug mode to visualize the mesh in the editor.     
        private bool meshInitialized = false;

        // Use this for initialization
        void Start()
        {

            lidarData = GameObject.Find("Dummy Lidar").GetComponent<Scanner>().lidarDataDict;
            numPoints = lidarData.size;
            mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;
            if (debug)
            {
                CreateMesh();
                meshInitialized = true;
            }

        }


        private void Update()
        {
            if (debug)
            {
                if (!meshInitialized)
                {
                    CreateMesh();
                    meshInitialized = true;
                }
                updateMesh();
            }
            else
            {
                if (meshInitialized)
                {
                    meshInitialized = false;
                    mesh.Clear();
                }
            }

        }

        //Updates mesh with new data from scanner.
        //Currently limited by Unity's 65k vertice limit.
        //try multi mesh next or interpolation next?
        void updateMesh()
        {
            for (int i = 0; i < points.Length; ++i)
            {
                float mag = points[i].magnitude;
                colors[i] = new Color((points[i].x / mag) + 0.5f, (points[i].y / mag) + 0.5f, 0, 1.0f); //Selects color of vertices and scales down. Should be moved to Shader asap.
            }
            mesh.colors = colors;
            points = lidarData.returnDictAsArray();
            mesh.vertices = points;

        }

        //Initializes mesh.
        void CreateMesh()
        {
            points = new Vector3[numPoints];
            indecies = new int[numPoints];
            colors = new Color[numPoints];
            points = lidarData.returnDictAsArray();
            for (int i = 0; i < points.Length; ++i)
            {
                indecies[i] = i;
                colors[i] = Color.white;
            }

            mesh.vertices = points;
            mesh.colors = colors;
            mesh.SetIndices(indecies, MeshTopology.Points, 0);

        }
    }
}