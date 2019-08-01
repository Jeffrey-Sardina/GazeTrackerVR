using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class MasterScene : MonoBehaviour
{
    Camera vrCam;
    static readonly Vector3 DISPLAY_LOCATION = new Vector3(0, 0, 10);
    const string PARADIGM_PATH = "Global/",
        IMAGE_PATH = "Global/UI Elements/Images/";

    // Start is called before the first frame update
    void Start()
    {
        vrCam = gameObject.GetComponent(typeof(Camera)) as Camera;
        DisplayImage("tp", DISPLAY_LOCATION + new Vector3(-5, 5, 0), 1);
        DisplayImage("cp", DISPLAY_LOCATION + new Vector3(-2.5f, 5, 0), 1);
        DisplayImage("vwp", DISPLAY_LOCATION + new Vector3(0, 5, 0), 1);
        DisplayImage("vcp", DISPLAY_LOCATION + new Vector3(2.5f, 5, 0), 1);
        DisplayImage("exit", DISPLAY_LOCATION + new Vector3(5, 5, 0), 1);
    }

    GameObject DisplayImage(string imageFileName, Vector3 location, float scale, string path = IMAGE_PATH)
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

        return imagePlane;
    }

    // Update is called once per frame
    void Update()
    {
        //Create a ray
        Ray ray = new Ray(vrCam.transform.position, vrCam.transform.forward);

        //Raycase: if there is a hit, log data about the hit
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform.name.Equals("tp"))
            {
                SceneManager.LoadScene("Theatre Paradigm");
            }
            else if (hit.transform.name.Equals("cp"))
            {
                SceneManager.LoadScene("Corner Paradigm");
            }
            else if (hit.transform.name.Equals("vwp"))
            {
                SceneManager.LoadScene("Visual Word Paradigm");
            }
            else if (hit.transform.name.Equals("vcp"))
            {
                SceneManager.LoadScene("Video-Corner Paradigm");
            }
            else if (hit.transform.name.Equals("exit"))
            {
                Application.Quit();
            }
        }
    }
}
