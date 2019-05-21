using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

public class TP_CameraController : MonoBehaviour
{
    #region instance data
    //Camera data
    Camera vrCam;

    //Experiment Data Collected
    LinkedList<string> frameDataList = new LinkedList<string>();

    //Output
    StreamWriter logger,
        writer;

    //Experiment constants
    readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 1.5f, 10);
    readonly int FIXATION_DOT_DISPLAY_TIME = 1,
        IMAGE_DISPLAY_TIME = 10,
        NUM_TRAINING_STIMULI = 0;
    const int IMAGE_PLANE_SIDE_LENGTH = 3,
        FRAME_DATA_BUFFER_SIZE = 5;
    const string FIXATION_DOT = "FixationDot",
        READY_DOT = "ReadyDot";
    const float IMAGE_SCALE = 20;

    //Experiment Management
    readonly List<string> STIMULUS_LIST = new List<string>();
    readonly LinkedList<GameObject> CURRENT_STIMULI = new LinkedList<GameObject>();
    GameObject text;
    System.Random GENERATOR = new System.Random();

    string dateTimeNow,
        experimentFileBaseName,
        experimentFileFullName,
        path,
        dataExtension,
        logExtension,
        experimentSpecificationsFileName,
        currentEvent;
    GameObject fixationDot,
        image,
        thankYou;
    float fixationDotEarliestContinuousHitTime = -1,
        readyDotEarliestContinuousHitTime = -1,
        imageDisplayStartTime,
        lastEventTriggerTime;
    bool fixationDotCallibrationDone,
        readyDotCallibrationDone,
        imageInitialDispayDone,
        calibrating = true,
        displaying,
        dataWritten,
        ended;
    int currentStimuliListIndex = 0;
    #endregion //instance data

    #region initialization functions
    void Start()
    {
        BeginExperiment();
    }

    void BeginExperiment()
    {
        ConfigureFileSystem();
        InitLogging();
        InitStimuliList();
        InitOutStream();
        fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
        recordData("Image loaction" + ExactPoint(DISPLAY_LOCATION), true);
        recordData("stimulus name,event,time,time since last event,x,y,z", true);
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;
    }

    void InitLogging()
    {
        logger = File.AppendText(path + experimentFileFullName + logExtension);
        Log("BeginExperiment: Logging started for current subject");
    }

    void ConfigureFileSystem()
    {
        dateTimeNow = getCleanDateTime();
        path = Application.persistentDataPath + "/";
        experimentFileBaseName = "ET_VR_TP";
        dataExtension = ".csv";
        logExtension = ".log";
        experimentFileFullName = experimentFileBaseName + dateTimeNow + dataExtension;
        experimentSpecificationsFileName = "EyeTrackerVRData_0";
    }

    void InitOutStream()
    {
        try
        {
            writer = new StreamWriter(path + experimentFileFullName);
        }
        catch (Exception ex)
        {
            Log("CameraController: WriteData: Failed to write data file!");
            Log(ex.StackTrace);
        }
    }

    void InitStimuliList()
    {
        List<string> trainingList = new List<string>();
        TextAsset data = Resources.Load<TextAsset>("Theatre Paradigm/experiments/" + experimentSpecificationsFileName);
        string data_text = data.text;
        foreach (string line in data_text.Split('\n'))
        {
            STIMULUS_LIST.Add(line.Trim());
        }

        //Load the first NUM_TRAINING_STIMULI elements from the simulus list as training elements only
        for (int i = 0; i < NUM_TRAINING_STIMULI; i++)
        {
            trainingList.Insert(0, STIMULUS_LIST[0]);
            STIMULUS_LIST.RemoveAt(0);
        }

        //Shuffle the STIMULUS_LIST, but not the training elements
        ShuffleList(STIMULUS_LIST);

        //Re-insert the training elements to the front of the STIMULUS_LIST
        for (int i = 0; i < NUM_TRAINING_STIMULI; i++)
        {
            STIMULUS_LIST.Insert(0, trainingList[0]);
            trainingList.RemoveAt(0);
        }
    }

    /// <summary>
    /// Uses the Fisher-Yates algorithm. Reference for the algorithm here:
    /// https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
    /// </summary>
    /// <param name="array"></param>
    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count - 2; i++)
        {
            int randomIndex = GENERATOR.Next(0, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    #endregion //initialization functions

    void Log(string message, string level = "info")
    {
        logger.WriteLine(level + " --- " + DateTime.Now.ToLongDateString() + " at " + DateTime.Now.ToLongTimeString());
        logger.WriteLine("Ecexution Time (s): " + Time.time);
        logger.WriteLine(message);
        logger.WriteLine("");
    }

    void OnApplicationQuit()
    {
        WriteData();
        writer.Close();
    }

    #region update functions
    void RunFixationDot()
    {
        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

        //Raycase: if there is a hit, log data about the hit
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            //Control fixation dot display time
            if (hit.transform.name.StartsWith(FIXATION_DOT))
            {
                //Still calibrating
                if (fixationDotEarliestContinuousHitTime < 0)
                {
                    fixationDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - fixationDotEarliestContinuousHitTime >= FIXATION_DOT_DISPLAY_TIME && !fixationDotCallibrationDone)
                {
                    Log("Update: Callibration (with the fixation dot) completed");
                    //Remove fixation dot 
                    fixationDotCallibrationDone = true;
                    Destroy(fixationDot);
                    fixationDot = null;
                    fixationDotEarliestContinuousHitTime = -1;
                    fixationDotCallibrationDone = false;

                    //Display image stimuli
                    calibrating = false;
                    currentEvent = "free looking";
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }
    }

    void RunExperiment()
    {
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            try
            {
                recordData(Time.fixedTime.ToString() + "," + (Time.time - lastEventTriggerTime) + "," + ExactPoint(hit.point));
            }
            catch (Exception ex)
            {
                recordData(Time.fixedTime.ToString() + "," + (Time.time - lastEventTriggerTime) + ",None");
            }
        }

        if (Time.time - imageDisplayStartTime > IMAGE_DISPLAY_TIME)
        {
            calibrating = true;
            displaying = false;
            Destroy(image);
        }
    }

    void Update()
    {
        if(calibrating)
        {
            if (!fixationDot)
            {
                currentEvent = "fixation dot-ing";
                fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
            }
            RunFixationDot();
        }
        else
        {
            if (currentStimuliListIndex >= STIMULUS_LIST.Count && !displaying)
            {
                if(!ended)
                {
                    DisplayEndMessage();
                    WriteData();
                    ended = true;
                }
            }
            else
            {
                if (!displaying)
                {
                    imageDisplayStartTime = Time.time;
                    image = DisplayImage(STIMULUS_LIST[currentStimuliListIndex], DISPLAY_LOCATION, IMAGE_SCALE);
                    currentStimuliListIndex++;
                    displaying = true;
                }
                RunExperiment();
            }
        }
    }
    #endregion //update functions

    #region display functions
    GameObject DisplayImage(string imageFileName, Vector3 location, float scale, string path = "Theatre Paradigm/stimuli/images/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject imagePlane = Instantiate(Resources.Load<GameObject>("Theatre Paradigm/ImagePlanePrefab"));
        imagePlane.transform.position = location;
        imagePlane.name = imageFileName;

        //Load the image onto the plane prefab
        Material image_material = new Material(Shader.Find("Diffuse"));
        Texture image_texture = Resources.Load<Texture>(path + imageFileName);

        Vector2 unitdim = new Vector2(image_texture.width, image_texture.height).normalized;
        Debug.Log(imagePlane.transform.localScale);
        Debug.Log(unitdim);
        Debug.Log(image_texture.width + " " + image_texture.height);
        Debug.Log(scale);
        imagePlane.transform.localScale = new Vector3(imagePlane.transform.localScale.x * unitdim.x * scale, imagePlane.transform.localScale.y * 1, imagePlane.transform.localScale.z * unitdim.y * scale);

        image_material.mainTexture = image_texture;
        imagePlane.GetComponent<Renderer>().material = image_material;

        recordData("Scaled size of " + imageFileName + ": (" + (unitdim.x * scale * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * scale * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return imagePlane;
    }

    GameObject DisplayFixationDot(Vector3 location, string name)
    {
        //Place the fixation dot
        GameObject dot = Instantiate(Resources.Load<GameObject>("Theatre Paradigm/" + FIXATION_DOT));
        dot.name = name;
        dot.transform.position = location;
        return dot;
    }

    void DisplayEndMessage()
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        thankYou = Instantiate(Resources.Load("Theatre Paradigm/Thank_You") as GameObject);
        thankYou.transform.position = DISPLAY_LOCATION;
    }
    #endregion //display functions

    #region data functions
    string getCleanDateTime()
    {
        return DateTime.Now.ToShortDateString().Replace('/', '-') + "__" + DateTime.Now.ToLongTimeString().Replace(':', '-');
    }

    string ExactPoint(Vector3 point)
    {
        return point.x + "," + point.y + "," + point.z;
    }

    void recordData(string data, bool isHeader = false)
    {
        if (isHeader)
        {
            frameDataList.AddLast(data);
            lastEventTriggerTime = Time.time;
        }
        else
        {
            string stimulus = image ? image.name : "None";
            frameDataList.AddLast(stimulus + "," + currentEvent + "," + data + ",");
        }
        if (frameDataList.Count >= FRAME_DATA_BUFFER_SIZE)
        {
            WriteData();
        }
    }

    void WriteData()
    {
        while(frameDataList.Count > 0)
        {
            writer.WriteLine(frameDataList.First.Value);
            frameDataList.RemoveFirst();
        }
        
    }
    #endregion //data functions
}
