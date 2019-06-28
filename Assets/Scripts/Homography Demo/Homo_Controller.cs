using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Features2dModule;

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
public class Homo_Controller : MonoBehaviour
{
    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat inMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat erodeMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat dilMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);

    private float blob_x;
    private float blob_y;
    private float blob_r;

    private float ray_x;
    private float ray_y;
    private float ray_r;

    public double THRESH_VAL = 150.0;
    public int K_ITERATIONS = 10;
    string circparam_path;
    private Mat struct_elt = new Mat (3, 3, CvType.CV_8UC1);

    [SerializeField]
    ARCameraManager m_ARCameraManager;
    public ARCameraManager cameraManager
    {
        get {return m_ARCameraManager; }
        set {m_ARCameraManager = value; }
    }

    [SerializeField]
    Text m_ImageInfo;
    public Text imageInfo
    {
        get { return m_ImageInfo; }
        set { m_ImageInfo = value; }
    }

    public Vector2 GetPos()
    {
        return new Vector2(ray_x, ray_y);
    }

    public float GetRad()
    {
        return ray_r;
    }

    void Awake()
    {
        Debug.Log("StartTest");
        Screen.autorotateToLandscapeLeft = true; 
        circparam_path = Utils.getFilePath("circparams.yml");
        m_ARRaycastManager = GetComponent<ARRaycastManager>();
    }

    void OnEnable()
    {
        if (m_ARCameraManager != null)
            m_ARCameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        if (m_ARCameraManager != null)
            m_ARCameraManager.frameReceived -= OnCameraFrameReceived;
    }

    void ComputerVisionAlgo(IntPtr greyscale) 
    {
        Utils.copyToMat(greyscale, imageMat);


        // Inverting Image pixel values
        inMat = (Mat.ones(imageMat.rows(), imageMat.cols(), CvType.CV_8UC1) * 255) - imageMat;

        // Creating Detector (Yellow Circle)
        // MatOfKeyPoint keyMat = new MatOfKeyPoint();
        // SimpleBlobDetector detector = SimpleBlobDetector.create();


        // Creating Detector (Red Circle)
        MatOfKeyPoint keyMat = new MatOfKeyPoint();
        SimpleBlobDetector detector = SimpleBlobDetector.create();
        inMat = imageMat;
        // detector.read(circparam_path);

        // Finding circles
        detector.detect(imageMat, keyMat);
        if (keyMat.size().height > 0)
        {
            blob_x = (float) keyMat.get(0, 0)[0];
            blob_y = (float) keyMat.get(0, 0)[1];
            blob_r = (float) keyMat.get(0, 0)[2];
        }

        // Visualizing detected circles
        // m_ImageInfo.text = 
        // Debug.Log(string.Format("Circle Count: {0}\n [IMAGE] blob_x: {1}\n blob_y: {2}\n blob_r: {3}", 
        // keyMat.size().height, blob_x, blob_y, blob_r));

        Features2d.drawKeypoints(imageMat, keyMat, outMat);
    }

    void FindRaycastPoint()
    {
        float w_ratio = (float)Screen.width/640;
        float h_ratio = (float)Screen.height/480;
        float scale = Math.Max(w_ratio, h_ratio);

        // Debug.Log(scale == w_ratio);

        ray_x = scale * blob_x;
        ray_y = 1080.0f - (3.375f * (blob_y - 80.0f));
        ray_r = scale * blob_r;

        m_ImageInfo.text = string.Format("{0} x {1}", ray_x, ray_y);

        // Debug.Log(string.Format("[SCREEN] ray_x: {0}\n ray_y: {1}\n ray_r: {2}", 
        // ray_x, ray_y, ray_r));
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // Camera data extraction
        XRCameraImage image;
        if (!cameraManager.TryGetLatestImage(out image))
        {
            Debug.Log("Uh Oh");
            return;
        }

        Vector2 img_dim = image.dimensions;
        XRCameraImagePlane greyscale = image.GetPlane(0);

        image.Dispose();

        // Process the image here: 
        unsafe {
            IntPtr greyPtr = (IntPtr) greyscale.data.GetUnsafePtr();
            ComputerVisionAlgo(greyPtr);
        }

        // Creates 3D object from image processing data
        FindRaycastPoint();
    }
    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;

    ARSessionOrigin m_SessionOrigin;
}
