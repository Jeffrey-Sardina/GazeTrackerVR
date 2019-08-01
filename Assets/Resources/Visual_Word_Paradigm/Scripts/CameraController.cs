using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

/// <summary>
/// This class manages the Unity Scene for a Visual Word Paradigm experiment. There are three main phases in this experiment paradigm:
///     Callibration
///         A fixation dot is shown for 1000ms and the subject is asked to look at it.
///     Data Collection
///         A visual stimulus is shown for 1000ms
///         An audio recording is played. 2000ms after the audio recording finishes, the scene transfers to the next stimulus.
///     Termination
///         Once all stimuli have run, the program write out the collected data and resets for the next subject.
/// </summary>
public class CameraController : MonoBehaviour
{
    #region instance data
    //Camera data
    Camera vrCam;

    //Experiment Data Collected
    LinkedList<string> frameDataList = new LinkedList<string>();

    //Logging
    StreamWriter writer;

    //Experiment constants
    static readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 0, 10),
        QI = DISPLAY_LOCATION + new Vector3(12, 8, 0),
        QII = DISPLAY_LOCATION + new Vector3(-12, 8, 0),
        QIV = DISPLAY_LOCATION + new Vector3(12, -8, 0),
        QIII = DISPLAY_LOCATION + new Vector3(-12, -8, 0),
        OFFSET = new Vector3(0, 0, -0.05f);
    const int FIXATION_DOT_DISPLAY_TIME = 1,
        READY_DOT_DISPLAY_TIME = 1,
        IMAGE_INITIAL_DISPLAY_TIME_PRE_READY_DOT = 3,
        POST_AUDIO_KEEP_DISPLAYING_IMAGES_TIME = 2,
        RESETTING_DISPLAY_TIME = 2,
        NUM_TRAINING_STIMULI = 2,
        IMAGE_PLANE_SIDE_LENGTH = 3,
        FRAME_DATA_BUFFER_SIZE = 10;
    const float IMAGE_SCALE = 3f;
    const string FIXATION_DOT = "FixationDot",
        READY_DOT = "ReadyDot",
        PARADIGM_PATH = "Visual_Word_Paradigm/",
        EXPERIMENT_FILE_BASE_NAME = "VWP_",
        DATA_EXTENSION = ".csv";

    //Experiment Management
    readonly List<string[]> STIMULUS_LIST = new List<string[]>();
    readonly LinkedList<GameObject> CURRENT_STIMULI = new LinkedList<GameObject>();
    GameObject text;
    System.Random GENERATOR = new System.Random();

    string experimentFileFullName,
        path,
        dataExtension,
        logExtension,
        experimentSpecificationsFileName,
        audioName = "",
        q1ImageName = "",
        q2ImageName = "",
        q3ImageName = "",
        q4ImageName = "",
        currentEvent = "",
        stimuliNames = ",,,,";
    GameObject fixationDot,
        readyDot,
        thankYou;
    AudioSource audioSource;
    float fixationDotEarliestContinuousHitTime = -1,
        readyDotEarliestContinuousHitTime = -1,
        imageDisplayStartTime,
        audioCompletionTime,
        resettingStartTime,
        lastEventTriggerTime;
    bool fixationDotCallibrationDone,
        readyDotCallibrationDone,
        imageInitialDispayDone,
        audioPlayingDone,
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

        //Initialize Camera and data collection
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;

        InitOutputStream();

        //Collect stimuli into sets that will be shown
        InitStimuliList();

        //Display the background and fixation dot to get started
        DisplayBackgroundPlane(DISPLAY_LOCATION);
        fixationDot = DisplayFixationDot(DISPLAY_LOCATION, FIXATION_DOT);
        currentEvent = "Beginning training";
        recordData("QI image,QII image,QIII image,QIV image,audio,stimulus ID,event,time,timeSinceLastEvent,x,y,z", true);
    }

    void ConfigureFileSystem()
    {
        path = Application.persistentDataPath + "/";
        experimentFileFullName = EXPERIMENT_FILE_BASE_NAME + getCleanDateTime() + DATA_EXTENSION;
        experimentSpecificationsFileName = "data";
    }

    void InitOutputStream()
    {
        try
        {
            writer = new StreamWriter(path + experimentFileFullName);
            writer.WriteLine("QI," + ExactPoint(QI));
            writer.WriteLine("QII," + ExactPoint(QII));
            writer.WriteLine("QIII," + ExactPoint(QIII));
            writer.WriteLine("QIV," + ExactPoint(QIV));
        }
        catch (Exception ex)
        {
            Debug.Log("CameraController: WriteData: Failed to write data file!");
            Debug.Log(ex.StackTrace);
        }
    }

    void InitStimuliList()
    {
        List<string[]> trainingList = new List<string[]>();
        TextAsset data = Resources.Load<TextAsset>(PARADIGM_PATH + "Experiment_Data/" + experimentSpecificationsFileName);
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

    #region display and audio functions
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
        Vector3 Q1 = QI + OFFSET;
        Vector3 Q2 = QII + OFFSET;
        Vector3 Q4 = QIV + OFFSET;
        Vector3 Q3 = QIII + OFFSET;

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

    void DisplayEndMessage()
    {
        //Instantiate the prefab plane for displaying images and set its loccation
        thankYou = Instantiate(Resources.Load(PARADIGM_PATH + "Thank_You") as GameObject);
        thankYou.transform.position = DISPLAY_LOCATION;
    }

    void PlayAudio()
    {
        audioSource = GetComponent<AudioSource>();
        audioName = STIMULUS_LIST[currentStimuliListIndex][4].Trim();
        audioSource.clip = Resources.Load<AudioClip>(PARADIGM_PATH + "stimuli/sounds/" + audioName);
        audioSource.Play();
    }
    #endregion //display and audio functions

    #region update functions
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
                    //Remove fixation dot 
                    fixationDotCallibrationDone = true;
                    fixationDot = null;

                    //Display image stimuli
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
                    //Remove fixation dot 
                    readyDotCallibrationDone = true;
                    Destroy(readyDot);

                    //play the audio--this is the part where we are interested in user eye movement
                    PlayAudio();

                    //Write timing data now that audio started
                    currentEvent = "audio started";
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
                readyDot = DisplayFixationDot(DISPLAY_LOCATION, READY_DOT);

                //Now we need to flag the initial image displat as donw
                imageInitialDispayDone = true;
            }
        }

        if (imageInitialDispayDone && readyDotCallibrationDone)
        {
            //Flag when audio has finished playing
            if (!audioSource.isPlaying && !audioPlayingDone)
            {
                audioPlayingDone = true;
                audioCompletionTime = Time.time;

                //Write timing data now that audio ended
                currentEvent = "audio ended ";
                audioName = "";
            }

            //After the audio has been done for a short time, end the experiment and rest for the next stimuli, if present
            if (audioPlayingDone && Time.time - audioCompletionTime >= POST_AUDIO_KEEP_DISPLAYING_IMAGES_TIME && !resetting)
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
                    ResetForNextRound();
                }
            }

            if (resetting && Time.time - resettingStartTime > RESETTING_DISPLAY_TIME)
            {
                //Quit
                Application.Quit();
            }
        }
    }

    bool RunStartUp()
    {
        if(text == null)
        {
            text = Instantiate(Resources.Load(PARADIGM_PATH + "trainingText") as GameObject);
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
                    fixationDotCallibrationDone = true;
                }
            }
            else if (!fixationDotCallibrationDone)
            {
                //Callibration failed, restart timer.
                fixationDotEarliestContinuousHitTime = -1;
            }
        }

        if(fixationDotCallibrationDone)
        {
            //Reset vars
            fixationDotEarliestContinuousHitTime = -1;
            fixationDotCallibrationDone = false;
            Destroy(text);
            text = null;
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
        if(currentStimuliListIndex == -1)
        {
            startUpOver = RunStartUp();
        }
        else if(currentStimuliListIndex == NUM_TRAINING_STIMULI && !trainingOver)
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
        audioSource = null;
        fixationDotEarliestContinuousHitTime =
            readyDotEarliestContinuousHitTime = -1;
        imageDisplayStartTime
            = audioCompletionTime
            = resettingStartTime
            = 0;
        fixationDotCallibrationDone
            = readyDotCallibrationDone
            = imageInitialDispayDone
            = audioPlayingDone
            = resetting
            = fullResetting
            = false;

        currentEvent = "Moving to next stimulus round";
    }

    void FullReset()
    {
        //Reset all vars to default values
        frameDataList.Clear();
        STIMULUS_LIST.Clear();
        fixationDot = null;
        readyDot = null;
        CURRENT_STIMULI.Clear();
        audioSource = null;
        fixationDotEarliestContinuousHitTime =
            readyDotEarliestContinuousHitTime = -1;
        imageDisplayStartTime
            = audioCompletionTime
            = resettingStartTime
            = 0;
        fixationDotCallibrationDone
            = readyDotCallibrationDone
            = imageInitialDispayDone
            = audioPlayingDone
            = resetting
            = fullResetting
            = false;
        currentStimuliListIndex = -1;
        Destroy(thankYou);

        //Begin the experiment again
        BeginExperiment();
    }
    #endregion //reset functions
}