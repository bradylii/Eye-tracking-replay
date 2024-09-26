using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class VisualizerManager : MonoBehaviour
{
    // Inspector Variables 
    [SerializeField] private string userID = "";
    [SerializeField] private bool emptyRoom = true;

    public GameObject rod;
    public GameObject EmptyRoom;
    public GameObject ObjectsRoom;
    public GameObject center;

    public GameObject gazePointIndicator;

    // Set variables depending on insceptor inputs
     private GameObject frame;
     private string fileName = "[Rod-Frame]Participant_";
     private string path;

    // Time Variables 
    private int onRow = 0;
    private bool isPaused = false;
    private float scrollPosition = 0f;
    private float scrollMax;
    private int trial = 0;
    
    // To keep track if time scroll left or right 
    private float lastScrollPosition = 0f;

    // Data Variables
    private StreamReader reader;
    private string[][] data;
    private float[][] floatData;
    private float frameAngle;
    private float rodAngle;
    
    // To keep track if room updated
    private float lastRoomAngle = 0;

    // Heatmap texture
    public int textureWidth = 512;
    public int textureHeight = 512;
    private Texture2D heatmapTexture;
    private Color[] heatmapColors;
    public GameObject heatmapPlane;

    // Heatmap data
    private int[,] heatmapData;
    private List<Vector3> gazePoints = new List<Vector3>();
    private float averageOriginY = 0;


    // Start is called before the first frame update
    void Start()
    {
        // Set up file name
        fileName = fileName + userID;

        // Configure Room and file to be empty or room with objects 
        if (emptyRoom)
        {
            fileName = fileName + " empty";
            ObjectsRoom.SetActive(false);
            frame = EmptyRoom;
        }
        else
        {
            EmptyRoom.SetActive(false);
            frame = ObjectsRoom;

            //non empty room smaller so move plane up 
            heatmapPlane.transform.position += new Vector3(0, 0, -2.65f); 
            rod.transform.position += new Vector3(0, 0, -1.0f);
        }

        path = Path.Combine(Application.streamingAssetsPath, "Logs/" + fileName + ".csv");
        Debug.Log("CSV Path: " + path);

        // Open the CSV file for reading
        if (File.Exists(path))
        {
            Debug.Log("File found");
            reader = new StreamReader(path);

            // set up the array of CSV data and convert to float
            InitData(path);

            // Set Camera Y position to random eye gaze origin y because our only estimate of where head position is 
            // The height varies per user and the rods were positioned relative to the users head
            // The users origin gaze Y stays mostly the same, example (1.52) 
            // so average the left and right and make head positon there
            Camera.main.transform.position += new Vector3(0, (floatData[2000][8] + floatData[2000][11]) / 2, 0);

            // Set Rod to be same Y position as line above 
            rod.transform.position += new Vector3(0, (floatData[2000][8] + floatData[2000][11]) / 2, 0);
            
        }
        else
        {
            Debug.LogError("CSV file not found at path: " + path);
        }
        

        // Initialize heatmap
        heatmapData = new int[textureWidth, textureHeight];
        heatmapTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        heatmapColors = new Color[textureWidth * textureHeight];

        // Apply the texture to the plane
        Renderer renderer = heatmapPlane.GetComponent<Renderer>();
        renderer.material.mainTexture = heatmapTexture;
    }


    // To Update the scene 
    void Update()
    {
        // This is the current time 
        onRow = (int)scrollPosition;
        
        // current Room and Rod angles
        frameAngle = floatData[onRow][0];
        rodAngle = floatData[onRow][1];

        // Press space to pause/resume the replay
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
            Debug.Log("resume/pause");
        }

        // Not paused and time not over 
        if (!isPaused && onRow < scrollMax)
        {

            //if new angle, get difference and put it in parameter 
            if (frameAngle != lastRoomAngle)
            {
                // Scrolling forward, update trial
                if (lastScrollPosition < scrollPosition) {
                    trial++;
                }

                // Scrolling forward, update trial
                else if (lastScrollPosition > scrollPosition) 
                {
                    trial--;
                }
                // The messed up rotateAround bug, so need to update like this
                frame.transform.RotateAround(center.transform.position, Vector3.forward, (frameAngle - lastRoomAngle));

                
            }

            // Set variables used to check previous to current
            lastRoomAngle = frameAngle;
            lastScrollPosition = onRow;

            // Rotate rods as user does
            rod.transform.eulerAngles = new Vector3(0, 0, rodAngle);

            // Draw the eye data
            DrawData();
            

            // Keep incrementing time 
            onRow++;
            scrollPosition++;
        }
        
        // Not paused and past the end 
        else if (!isPaused)
        {
            Debug.Log("End offile reached.");
        }
        Debug.Log(onRow);
    }


    // Store in array data ["time"][columns/angles]
    // Convert everything to floats and remove the header
    // Use this array to access data easily 
    void InitData(string filePath)
    {
        using (StreamReader reader = new StreamReader(filePath))
        {
            string[] allLines = File.ReadAllLines(filePath);

            // The number of rows there are, used to measure max time too
            scrollMax = allLines.Length;

            // Initialize the data array with a string for the header and floats for the rest
            data = new string[(int)scrollMax][];
            floatData = new float[(int)scrollMax - 1][];

            // Read (skip) the header line
            string headerLine = reader.ReadLine();
            data[0] = headerLine.Split(',');

            // Read the rest of the lines
            for (int i = 1; i < scrollMax; i++)
            {
                string line = reader.ReadLine();
                string[] columns = line.Split(',');

                floatData[i - 1] = new float[columns.Length];

                // Convert each column to float and store in floatData
                for (int k = 0; k < columns.Length; k++)
                {
                    floatData[i - 1][k] = float.Parse(columns[k]);
                }

            }
        }
    }

    // GUI Scrollable Video and shows info
    void OnGUI()
    {
        GUILayout.BeginHorizontal();

        // Scrollbar to scroll through the timeline
        scrollPosition = GUILayout.HorizontalScrollbar(scrollPosition, 5f, 0f, scrollMax, GUILayout.Width(400));

        GUILayout.EndHorizontal();

        // Display the scroll position (e.g., current time in the video)
        GUILayout.Label("Current Time: " + scrollPosition.ToString("F2"));
        GUILayout.Label("ParticipantID: " + userID);
        GUILayout.Label("Trial: " + trial + "/20");

    }

    
    Vector3 FindIntersection(Vector3 leftPos, Vector3 leftDir, Vector3 rightPos, Vector3 rightDir)
    {
        Vector3 p1 = leftPos;
        Vector3 d1 = leftDir.normalized;
        Vector3 p2 = rightPos;
        Vector3 d2 = rightDir.normalized;

        Vector3 p2_p1 = p2 - p1;
        float a = Vector3.Dot(d1, d1);
        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d2, d2);
        float d = Vector3.Dot(d1, p2_p1);
        float e = Vector3.Dot(d2, p2_p1);

        float denominator = a * c - b * b;

        if (Mathf.Abs(denominator) < Mathf.Epsilon)
        {
            // Lines are parallel
            return (p1 + p2) / 2.0f;
        }

        float t = (d * c - b * e) / denominator;
        float u = (a * e - b * d) / denominator;

        Vector3 pointOnLeft = p1 + t * d1;
        Vector3 pointOnRight = p2 + u * d2;

        return (pointOnLeft + pointOnRight) / 2.0f;
    }

    // To Draw eye data onto heatmap 
    void DrawData()
    {
        // Clear previous heatmap data
        System.Array.Clear(heatmapData, 0, heatmapData.Length);
        gazePoints.Clear();
        

        // Update heatmap data with gaze points
        for (int i = 0; i < onRow && trial <= 20; i++)
        {
            // Use gaze coordinates 
            //             (leftGazeX)        (rightGazeX)
            float gazeX = (floatData[i][13] + floatData[i][16]) / 2;
            //             (leftGazeY)        (rightGazeY)
            float gazeY = (floatData[i][14] + floatData[i][17]) / 2;

            // Convert gaze coordinates to texture coordinates
            int x = Mathf.Clamp((int)((gazeX + 1) * 0.5f * textureWidth), 0, textureWidth - 1);
            int y = Mathf.Clamp((int)((gazeY + 1) * 0.5f * textureHeight), 0, textureHeight - 1);
            //x = gazeX;
            //y += 50;

            heatmapData[x, y]++;
            Vector3 leftGaze = new Vector3(floatData[i][13], floatData[i][14], floatData[i][15]);
            Vector3 rightGaze = new Vector3(floatData[i][16], floatData[i][17], floatData[i][18]);

            Vector3 leftOrigin = new Vector3(floatData[i][7], floatData[i][8], floatData[i][9]);
            Vector3 rightOrigin = new Vector3(floatData[i][10], floatData[i][11], floatData[i][12]);

            Vector3 intersection = FindIntersection(leftOrigin, leftGaze, rightOrigin, rightGaze);

            gazePoints.Add(intersection);
        }
        Vector3 currentleftGaze = new Vector3(floatData[onRow][13], floatData[onRow][14], floatData[onRow][15]);
        Vector3 currentrightGaze = new Vector3(floatData[onRow][16], floatData[onRow][17], floatData[onRow][18]);

        Vector3 currentleftOrigin = new Vector3(floatData[onRow][7], floatData[onRow][8], floatData[onRow][9]);
        Vector3 currentrightOrigin = new Vector3(floatData[onRow][10], floatData[onRow][11], floatData[onRow][12]);

        Vector3 currentintersection = FindIntersection(currentleftOrigin, currentleftGaze, currentrightOrigin, currentrightGaze);
        currentleftGaze += new Vector3(0, Camera.main.transform.position.y, 0);
        currentrightGaze += new Vector3(0, Camera.main.transform.position.y, 0);


        gazePointIndicator.transform.position = (currentleftGaze + currentrightGaze) / 2;//currentintersection;

        // Update heatmap texture
        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                float value = heatmapData[x, y];
                heatmapColors[x + y * textureWidth] = Color.Lerp(Color.white, Color.blue, value / 10f);
            }
        }
        
        // // Apply heatmap
        // heatmapTexture.SetPixels(heatmapColors);
        // heatmapTexture.Apply();
        
        
        // foreach (var point in gazePoints)
        // {
        //     Vector2 uv = new Vector2(point.x, point.y); // Assuming gaze points are normalized to [0, 1]
        //     int x = Mathf.FloorToInt(uv.x * textureWidth);
        //     int y = Mathf.FloorToInt(uv.y * textureHeight);
        //     int index = y * textureWidth + x;

        //     if (index >= 0 && index < heatmapColors.Length)
        //     {
        //         heatmapColors[index] += new Color(1, 0, 0, 1); // Red color for intensity
        //     }
        // }

        heatmapTexture.SetPixels(heatmapColors);
        heatmapTexture.Apply();
    }

}
