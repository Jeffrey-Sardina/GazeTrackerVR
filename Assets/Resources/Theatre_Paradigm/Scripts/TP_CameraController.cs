using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Specialized;

public class TP_CameraController : MonoBehaviour
{
    #region instance data
    //Camera data
    Camera vrCam;

    //Experiment Data Collected
    LinkedList<string> frameDataList = new LinkedList<string>();

    //Output
    StreamWriter writer;

    //Experiment constants
    static readonly Vector3 DISPLAY_LOCATION_CENTER = new Vector3(0, 0, 10);
    static readonly int FIXATION_DOT_DISPLAY_TIME = 1,
        IMAGE_DISPLAY_TIME = 10,
        NUM_TRAINING_STIMULI = 0;
    const int IMAGE_PLANE_SIDE_LENGTH = 3,
        FRAME_DATA_BUFFER_SIZE = 5;
    const string FIXATION_DOT = "FixationDot",
        READY_DOT = "ReadyDot";
    const float IMAGE_SCALE = 20;

    //Experiment Management
    readonly List<string> STIMULUS_LIST = new List<string>();
    readonly List<object[]> STIMULUS_META = new List<object[]>();
    readonly LinkedList<GameObject> CURRENT_STIMULI = new LinkedList<GameObject>();
    GameObject text;
    System.Random GENERATOR = new System.Random();
    Vector3 curr_scaled_img_bottom_left,
        curr_scaled_img_scale;

    const string DATA_EXTENSION = ".csv",
        EXPERIMENT_FILE_BASE_NAME = "TP_";
    string experimentFileFullName,
        path,
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
        InitStimuliList();
        InitOutStream();
        fixationDot = DisplayFixationDot(FixationDotLocForCurrentStimulus(), FIXATION_DOT);
        recordData("Image loaction" + ExactPoint(DISPLAY_LOCATION_CENTER), true);
        recordData("stimulus name,event,time,time since last event,x,y,z", true);
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;
    }

    void ConfigureFileSystem()
    {
        path = Application.persistentDataPath + "/";
        experimentFileFullName = EXPERIMENT_FILE_BASE_NAME + getCleanDateTime() + DATA_EXTENSION;
        experimentSpecificationsFileName = "data";
    }

    void InitOutStream()
    {
        try
        {
            writer = new StreamWriter(path + experimentFileFullName);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.Log("CameraController: WriteData: Failed to write data file!");
            UnityEngine.Debug.Log(ex.StackTrace);
        }
    }

    /// <summary>
    /// Initialized the stimulus list and stimulus_meta dictionary by loading data from a CSV file.
    /// Input data should be in CSV format; lines beginning with '#' are ignored.The file should have the following order of items:
    /// 0 - pic (string)
    /// 1 - x (int)
    /// 2 - y (int)
    /// 3 - focus (strong)
    /// 4 - animacy (string)
    /// 5 - event (string)
    /// 6 - side (string)
    /// 7 - an_hierarchy (string)
    /// 8 - x_ratio (float)
    /// 9 - y_ratio (float)
    /// </summary>
    void InitStimuliList()
    {
        TextAsset data = Resources.Load<TextAsset>("Theatre_Paradigm/Experiment_Data/" + experimentSpecificationsFileName);
        string data_text = data.text;
        foreach (string line in data_text.Split('\n'))
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            object[] meta_list = new object[9];
            string cleaned = line.Trim();
            string[] elements = cleaned.Split(',');
            
            for (int i = 0; i < cleaned.Length; i++)
            {
                switch (i)
                {
                    case 1:
                        meta_list[i - 1] = Int32.Parse(elements[i]);
                        break;
                    case 2:
                        meta_list[i - 1] = Int32.Parse(elements[i]);
                        break;
                    case 3:
                        meta_list[i - 1] = elements[i];
                        break;
                    case 4:
                        meta_list[i - 1] = elements[i];
                        break;
                    case 5:
                        meta_list[i - 1] = elements[i];
                        break;
                    case 6:
                        meta_list[i - 1] = elements[i];
                        break;
                    case 7:
                        meta_list[i - 1] = elements[i];
                        break;
                    case 8:
                        meta_list[i - 1] = float.Parse(elements[i]);
                        break;
                    case 9:
                        meta_list[i - 1] = float.Parse(elements[i]);
                        break;
                }
            }
            string stimulus = elements[0].Split('.')[0];
            STIMULUS_LIST.Add(stimulus); //Do not add image extension
            STIMULUS_META.Add(meta_list);
        }

        //Load the first NUM_TRAINING_STIMULI elements from the simulus list as training elements only
        List<string> trainingList = new List<string>();
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

        UnityEngine.Debug.Log(STIMULUS_LIST);
        UnityEngine.Debug.Log(STIMULUS_META);
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
        if (calibrating)
        {
            if (!fixationDot)
            {
                currentEvent = "fixation dot-ing";
                fixationDot = DisplayFixationDot(FixationDotLocForCurrentStimulus(), FIXATION_DOT);
            }
            RunFixationDot();
        }
        else
        {
            if (currentStimuliListIndex >= STIMULUS_LIST.Count && !displaying)
            {
                if (!ended)
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
                    image = DisplayImage(STIMULUS_LIST[currentStimuliListIndex]);
                    currentStimuliListIndex++;
                    displaying = true;
                }
                RunExperiment();
            }
        }
    }
    #endregion //update functions

    #region display functions
    GameObject DisplayImage(string imageFileName, string path = "Theatre_Paradigm/stimuli/images/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject imagePlane = Instantiate(Resources.Load<GameObject>("Theatre_Paradigm/ImagePlanePrefab"));
        imagePlane.transform.position = DISPLAY_LOCATION_CENTER;
        imagePlane.name = imageFileName;

        //Load the image onto the plane prefab
        Material image_material = new Material(Shader.Find("Diffuse"));
        Texture image_texture = Resources.Load<Texture>(path + imageFileName);

        Vector2 unitdim = new Vector2(image_texture.width, image_texture.height).normalized;
        imagePlane.transform.localScale = new Vector3(imagePlane.transform.localScale.x * unitdim.x * IMAGE_SCALE, imagePlane.transform.localScale.y * 1, imagePlane.transform.localScale.z * unitdim.y * IMAGE_SCALE);

        image_material.mainTexture = image_texture;
        imagePlane.GetComponent<Renderer>().material = image_material;

        curr_scaled_img_bottom_left = imagePlane.transform.position - (new Vector3(unitdim.x * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, unitdim.y * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, 0) / 2);
        curr_scaled_img_scale = new Vector3(unitdim.x * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, unitdim.y * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, 0);

        recordData("Scaled size of " + imageFileName + ": (" + (unitdim.x * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return imagePlane;
    }

    GameObject DisplayFixationDot(Vector3 location, string name)
    {
        //Place the fixation dot
        GameObject dot = Instantiate(Resources.Load<GameObject>("Theatre_Paradigm/" + FIXATION_DOT));
        dot.name = name;
        dot.transform.position = location;
        return dot;
    }

    void DisplayEndMessage()
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        thankYou = Instantiate(Resources.Load("Theatre_Paradigm/Thank_You") as GameObject);
        thankYou.transform.position = DISPLAY_LOCATION_CENTER;
    }
    #endregion //display functions

    #region data functions
    string getCleanDateTime()
    {
        return DateTime.Now.ToShortDateString().Replace('/', '-') + "__" + DateTime.Now.ToLongTimeString().Replace(':', '-');
    }

    Vector3 FixationDotLocForCurrentStimulus(string path = "Theatre_Paradigm/stimuli/images/")
    {
        Texture image_texture = Resources.Load<Texture>(path + STIMULUS_LIST[currentStimuliListIndex]);
        Vector2 unitdim = new Vector2(image_texture.width, image_texture.height).normalized;

        curr_scaled_img_bottom_left = DISPLAY_LOCATION_CENTER - (new Vector3(unitdim.x * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, unitdim.y * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, 0) / 2);
        curr_scaled_img_scale = new Vector3(unitdim.x * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, unitdim.y * IMAGE_SCALE * IMAGE_PLANE_SIDE_LENGTH, 0);

        Vector3 relative_offset = new Vector3(((float) STIMULUS_META[currentStimuliListIndex][7]) * curr_scaled_img_scale.x, ((float) STIMULUS_META[currentStimuliListIndex][8]) * curr_scaled_img_scale.y, 0);
        return curr_scaled_img_bottom_left + relative_offset;
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
        while (frameDataList.Count > 0)
        {
            writer.WriteLine(frameDataList.First.Value);
            frameDataList.RemoveFirst();
        }

    }
    #endregion //data functions
}
