using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using UnityEngine.Video;

/// <summary>
/// This class manages the Unity Scene for a Visual Word Paradigm experiment. There are three main phases in this experiment paradigm:
///     Callibration
///         A fixation dot is shown for 1000ms and the subject is asked to look at it.
///     Data Collection
///         A visual stimulus is shown for 1000ms
///         A video recording is played. 2000ms after the video recording finishes, the scene transfers to the next stimulus.
///     Termination
///         Once all stimuli have run, the program write out the collected data and resets for the next subject.
/// </summary>
public class VCP_CameraController : MonoBehaviour
{
    #region instance data
    //Camera data
    Camera vrCam;

    //Experiment Data Collected
    LinkedList<string> frameDataList = new LinkedList<string>();

    //Logging
    StreamWriter logger,
        writer;

    //Experiment constants
    const string PARADIGM_PATH = "Video-Corner Paradigm/";
    readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 1.5f, 10);
    const int FIXATION_DOT_DISPLAY_TIME = 1,
        READY_DOT_DISPLAY_TIME = 1,
        IMAGE_INITIAL_DISPLAY_TIME_PRE_READY_DOT = 3,
        POST_VIDEO_KEEP_DISPLAYING_IMAGES_TIME = 5,
        RESETTING_DISPLAY_TIME = 2,
        NUM_TRAINING_STIMULI = 2,
        IMAGE_PLANE_SIDE_LENGTH = 3,
        FRAME_DATA_BUFFER_SIZE = 10;
    const float IMAGE_SCALE = 3f;
    const string FIXATION_DOT = "FixationDot",
        READY_DOT = "ReadyDot";
    readonly Vector3 QI = new Vector3(9, 7, 0),
        QII = new Vector3(-9, 7, 0),
        QIV = new Vector3(9, -7, 0),
        QIII = new Vector3(-9, -7, 0);

    //Experiment Management
    readonly List<string[]> STIMULUS_LIST = new List<string[]>();
    readonly List<string> VIDEO_LIST = new List<string>();
    readonly LinkedList<GameObject> CURRENT_STIMULI = new LinkedList<GameObject>();
    GameObject text,
        videoPlane;
    System.Random GENERATOR = new System.Random();

    string dateTimeNow,
        experimentFileBaseName,
        experimentFileFullName,
        path,
        dataExtension,
        logExtension,
        experimentSpecificationsFileName,
        videoName = "",
        q1ImageName = "",
        q2ImageName = "",
        q3ImageName = "",
        q4ImageName = "",
        currentEvent = "",
        stimuliNames = ",,,,";
    GameObject fixationDot,
        readyDot,
        thankYou;
    VideoPlayer videoPlayer;
    float fixationDotEarliestContinuousHitTime = -1,
        readyDotEarliestContinuousHitTime = -1,
        imageDisplayStartTime,
        videoCompletionTime,
        resettingStartTime,
        lastEventTriggerTime;
    bool fixationDotCallibrationDone,
        readyDotCallibrationDone,
        imageInitialDispayDone,
        videoDisplayDone,
        videoPlayingDone,
        resetting,
        fullResetting,
        dataWritten = false,
        trainingOver,
        startUpOver;
    int currentStimuliListIndex = -1;
    #endregion //instance data

    #region initialization functions
    void Start()
    {
        BeginExperiment();
    }

    void BeginExperiment()
    {
        //Configure file system info
        ConfigureFileSystem();

        //Init logging
        logger = File.AppendText(path + experimentFileFullName + logExtension);
        Log("BeginExperiment: Logging started for current subject");

        //Initialize Camera and data collection
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;

        InitOutputStream();

        //Collect stimuli into sets that will be shown
        InitStimuliList();

        //Display the background and fixation dot to get started
        DisplayBackgroundPlane(DISPLAY_LOCATION + new Vector3(0, 0, .01f)); //Offset a tad
        Log("BeginExperiment: Initialization complete");
        currentEvent = "Beginning training";
        recordData("QI image,QII image,QIII image,QIV image,video,stimulus ID,event,time,timeSinceLastEvent,x,y,z", true);
    }

    void ConfigureFileSystem()
    {
        dateTimeNow = getCleanDateTime();
        path = Application.persistentDataPath + "/";
        experimentFileBaseName = "ET_VR_";
        dataExtension = ".csv";
        logExtension = ".log";
        experimentFileFullName = experimentFileBaseName + dateTimeNow + dataExtension;
        experimentSpecificationsFileName = "EyeTrackerVRData_0";
    }

    void InitOutputStream()
    {
        try
        {
            writer = new StreamWriter(path + experimentFileFullName);
            writer.WriteLine("QI," + ExactPoint(DISPLAY_LOCATION + QI));
            writer.WriteLine("QII," + ExactPoint(DISPLAY_LOCATION + QII));
            writer.WriteLine("QIII," + ExactPoint(DISPLAY_LOCATION + QIII));
            writer.WriteLine("QIV," + ExactPoint(DISPLAY_LOCATION + QIV));
        }
        catch (Exception ex)
        {
            Log("CameraController: WriteData: Failed to write data file!");
            Log(ex.StackTrace);
        }
    }

    void InitStimuliList()
    {
        List<string[]> trainingList = new List<string[]>();
        TextAsset data = Resources.Load<TextAsset>(PARADIGM_PATH + "experimental_data/" + experimentSpecificationsFileName);
        string data_text = data.text;
        foreach (string line in data_text.Split('\n'))
        {
            //Split line data by entry
            string[] stimuliSet = line.Split(',');

            //Get the images
            int numNonImages = 2;
            string[] images = new string[stimuliSet.Length - numNonImages];
            for (int i = 0; i < stimuliSet.Length - numNonImages; i++)
            {
                images[i] = stimuliSet[i];
            }

            //Randomly shuffle the images
            ShuffleArray<string>(images);

            //Put the new order into the stimuliSet
            for (int i = 0; i < images.Length && i < stimuliSet.Length; i++)
            {
                stimuliSet[i] = images[i];
            }

            //Add the shuffled data to the STIMULUS_LIST
            STIMULUS_LIST.Add(stimuliSet);
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
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    void ShuffleArray<T>(T[] array)
    {
        for (int i = 0; i < array.Length - 2; i++)
        {
            int randomIndex = GENERATOR.Next(0, array.Length);
            T temp = array[i];
            array[i] = array[randomIndex];
            array[randomIndex] = temp;
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

    void OnApplicationQuit()
    {
        WriteData();
        writer.Close();
    }

    void Log(string message, string level = "info")
    {
        logger.WriteLine(level + " --- " + DateTime.Now.ToLongDateString() + " at " + DateTime.Now.ToLongTimeString());
        logger.WriteLine("Ecexution Time (s): " + Time.time);
        logger.WriteLine(message);
        logger.WriteLine("");
    }

    #region display functions
    GameObject DisplayFixationDot(Vector3 location, string name)
    {
        //Place the fixation dot
        GameObject dot = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + FIXATION_DOT));
        dot.name = name;
        dot.transform.position = location;
        return dot;
    }

    GameObject DisplayBackgroundPlane(Vector3 location)
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject backgroudPlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "BackgroundPlanePrefab"));
        backgroudPlane.transform.position = location;
        backgroudPlane.name = "backgroundPlane";

        //Return the instantiated GameObject
        return backgroudPlane;
    }

    GameObject DisplayImage(string imageFileName, Vector3 location, float scale, string path = PARADIGM_PATH + "stimuli/images/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject imagePlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "ImagePlanePrefab"));
        imagePlane.transform.position = location;
        imagePlane.name = imageFileName;

        //Load the image onto the plane prefab
        Material image_material = new Material(Shader.Find("Diffuse"));
        Texture2D image_texture = Resources.Load<Texture2D>(path + imageFileName);

        Vector2 unitdim = new Vector2(image_texture.width, image_texture.height).normalized;
        imagePlane.transform.localScale = new Vector3(imagePlane.transform.localScale.x * unitdim.x * scale, imagePlane.transform.localScale.y * 1, imagePlane.transform.localScale.z * unitdim.y * scale);

        image_material.mainTexture = image_texture;
        imagePlane.GetComponent<Renderer>().material = image_material;

        recordData("Scaled size of " + imageFileName + ": (" + (unitdim.x * scale * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * scale * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return imagePlane;
    }

    void DisplayStimuli()
    {
        Vector3 Q1 = DISPLAY_LOCATION + QI;
        Vector3 Q2 = DISPLAY_LOCATION + QII;
        Vector3 Q4 = DISPLAY_LOCATION + QIV;
        Vector3 Q3 = DISPLAY_LOCATION + QIII;

        q1ImageName = STIMULUS_LIST[currentStimuliListIndex][0];
        q2ImageName = STIMULUS_LIST[currentStimuliListIndex][1];
        q3ImageName = STIMULUS_LIST[currentStimuliListIndex][2];
        q4ImageName = STIMULUS_LIST[currentStimuliListIndex][3];

        CURRENT_STIMULI.AddFirst(DisplayImage(q1ImageName, Q1, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q2ImageName, Q2, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q3ImageName, Q3, IMAGE_SCALE));
        CURRENT_STIMULI.AddFirst(DisplayImage(q4ImageName, Q4, IMAGE_SCALE));
        imageDisplayStartTime = Time.time;
    }

    GameObject DisplayVideo(string videoFileName, Vector3 location, float scale, string path = PARADIGM_PATH + "stimuli/videos/")
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        GameObject videoPlane = Instantiate(Resources.Load<GameObject>(PARADIGM_PATH + "ImagePlanePrefab"));
        videoPlane.transform.position = location;
        videoPlane.name = videoFileName;

        //Load the vidoe onto the plane prefab
        videoPlayer = videoPlane.GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoOver;
        VideoClip clip = Resources.Load<VideoClip>(path + videoFileName);
        videoPlayer.clip = clip;

        Vector2 unitdim = new Vector2(videoPlayer.clip.width, videoPlayer.clip.height).normalized;
        videoPlane.transform.localScale = new Vector3(videoPlane.transform.localScale.x * unitdim.x * scale, videoPlane.transform.localScale.y * 1, videoPlane.transform.localScale.z * unitdim.y * scale);

        recordData("Scaled size of " + videoFileName + ": (" + (unitdim.x * scale * IMAGE_PLANE_SIDE_LENGTH) + " : " + (unitdim.y * scale * IMAGE_PLANE_SIDE_LENGTH) + ")", true);
        return videoPlane;
    }

    void DisplayEndMessage()
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        thankYou = Instantiate(Resources.Load(PARADIGM_PATH + "Thank_You") as GameObject);
        thankYou.transform.position = DISPLAY_LOCATION;
    }
    #endregion //display and video functions

    #region update functions
    void OnVideoOver(VideoPlayer vp)
    {
        Log("Update: Video done playing");
        videoPlayingDone = true;
        Destroy(videoPlane);
        videoCompletionTime = Time.time;

        //Write timing data now that video ended
        currentEvent = "video ended ";
        videoName = "";
        if (fixationDot == null)
        {
            fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
        }
    }

    void RunExperiment()
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

                    //Display video stimulus
                    DisplayStimuli();
                    currentEvent = "free looking";
                    stimuliNames = q1ImageName + "," + q2ImageName + "," + q3ImageName + "," + q4ImageName + "," + STIMULUS_LIST[currentStimuliListIndex][4];
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
            else if (hit.transform.name.StartsWith(READY_DOT))
            {
                //Still calibrating
                if (readyDotEarliestContinuousHitTime < 0)
                {
                    readyDotEarliestContinuousHitTime = Time.time;
                }
                else if (Time.time - readyDotEarliestContinuousHitTime >= READY_DOT_DISPLAY_TIME && !readyDotCallibrationDone)
                {
                    Log("Update: Callibration (with the ready dot) completed: playing video");

                    //Remove ready dot 
                    readyDotCallibrationDone = true;
                    Destroy(readyDot);

                    //play the video--this is the part where we are interested in user eye movement
                    videoPlane = DisplayVideo(STIMULUS_LIST[currentStimuliListIndex][4], DISPLAY_LOCATION, IMAGE_SCALE);
                    Destroy(fixationDot);
                    fixationDot = null;

                    //Write timing data now that video started
                    currentEvent = "video started";
                }
            }
            else if (!readyDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                readyDotEarliestContinuousHitTime = -1;
            }

            try
            {
                recordData(Time.fixedTime.ToString() + "," + (Time.time - lastEventTriggerTime) + "," + ExactPoint(hit.point));
            }
            catch (Exception ex)
            {
                recordData(Time.fixedTime.ToString() + "," + (Time.time - lastEventTriggerTime) + ",None");
            }
        }

        //Control the display of the next stimulus once callibration is done
        if (fixationDotCallibrationDone)
        {
            if (Time.time - imageDisplayStartTime >= IMAGE_INITIAL_DISPLAY_TIME_PRE_READY_DOT && !imageInitialDispayDone)
            {
                Log("Update: Initial image display done, ready to show readyDot");
                readyDot = DisplayFixationDot(DISPLAY_LOCATION, READY_DOT);

                //Now we need to flag the initial image displat as donw
                imageInitialDispayDone = true;
            }
        }

        if (imageInitialDispayDone && readyDotCallibrationDone)
        {
            //After the video has been done for a short time, end the experiment and rest for the next stimuli, if present
            if (videoPlayingDone && Time.time - videoCompletionTime >= POST_VIDEO_KEEP_DISPLAYING_IMAGES_TIME && !resetting)
            {
                //Increment the index is the stimuli list
                currentStimuliListIndex++;

                //Remove stimuli game objects
                foreach (GameObject go in CURRENT_STIMULI)
                {
                    Destroy(go);
                }
                stimuliNames = ",,,,";

                //If there are not more prompts, prepare to terminate
                if (currentStimuliListIndex >= STIMULUS_LIST.Count)
                {
                    Log("Update: Program full-reset protocal beginning");

                    //Set termination flags
                    resetting = true;
                    resettingStartTime = Time.time;

                    //Write out all collected data to disk
                    WriteData();
                    dataWritten = true;

                    //Display a thank-you message
                    DisplayEndMessage();
                }
                //Else, reset for next round, with the same subject
                else
                {
                    Log("Update: Resetting for the next round of images with the same subject");
                    ResetForNextRound();
                }
            }
        }
    }

    bool RunStartUp()
    {
        if (videoPlane == null)
        {
            videoPlane = DisplayVideo("VocabInstructions", DISPLAY_LOCATION, IMAGE_SCALE);
            fixationDot = DisplayFixationDot(DISPLAY_LOCATION + new Vector3(0f, 3f, 0f), FIXATION_DOT);
        }

        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

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
                    fixationDotCallibrationDone = true;
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }

        if (fixationDotCallibrationDone)
        {
            //Reset vars
            //Destroy(fixationDot);
            fixationDotEarliestContinuousHitTime = -1;
            fixationDotCallibrationDone = false;
            Destroy(videoPlane);
            videoPlane = null;
            currentStimuliListIndex = 0;
            return true;
        }
        return false;
    }

    bool RunTrainingOver()
    {
        if (text == null)
        {
            text = Instantiate(Resources.Load(PARADIGM_PATH + "studyText") as GameObject);
            text.transform.position = DISPLAY_LOCATION;
        }

        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

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
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }

        if (fixationDotCallibrationDone)
        {
            //Cleanup
            currentEvent = "Beginning experiment (training ended)";
            Destroy(text);
            text = null;
            fixationDotCallibrationDone = false;
            return true;
        }
        return false;
    }

    void Update()
    {
        //RunExperiment();
        if (currentStimuliListIndex == -1)
        {
            startUpOver = RunStartUp();
        }
        else if (currentStimuliListIndex == NUM_TRAINING_STIMULI && !trainingOver)
        {
            if (fixationDot == null)
            {
                fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
                fixationDotEarliestContinuousHitTime = -1;
                fixationDotCallibrationDone = false;
            }
            trainingOver = RunTrainingOver();
        }

        if ((startUpOver && currentStimuliListIndex < NUM_TRAINING_STIMULI) || trainingOver)
        {
            RunExperiment();
        }
    }
    #endregion //update functions

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
            frameDataList.AddLast(stimuliNames + "," + (currentStimuliListIndex + 1) + "," + currentEvent + "," + data + ",");
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

    #region reset functions
    void ResetForNextRound()
    {
        videoPlayer = null;
        fixationDotEarliestContinuousHitTime =
            readyDotEarliestContinuousHitTime = -1;
        imageDisplayStartTime
            = videoCompletionTime
            = resettingStartTime
            = 0;
        fixationDotCallibrationDone
            = readyDotCallibrationDone
            = imageInitialDispayDone
            = videoPlayingDone
            = resetting
            = fullResetting
            = false;

        currentEvent = "Moving to next stimulus round";
        Log("ResetForNextRound: reset complete");
    }

    void FullReset()
    {
        //Reset all vars to default values
        frameDataList.Clear();
        STIMULUS_LIST.Clear();
        fixationDot = null;
        readyDot = null;
        CURRENT_STIMULI.Clear();
        videoPlayer = null;
        fixationDotEarliestContinuousHitTime =
            readyDotEarliestContinuousHitTime = -1;
        imageDisplayStartTime
            = videoCompletionTime
            = resettingStartTime
            = 0;
        fixationDotCallibrationDone
            = readyDotCallibrationDone
            = imageInitialDispayDone
            = videoPlayingDone
            = resetting
            = fullResetting
            = false;
        currentStimuliListIndex = -1;
        Destroy(thankYou);


        Log("FullReset: reset complete");

        //Begin the experiment again
        BeginExperiment();
    }
    #endregion //reset functions
}