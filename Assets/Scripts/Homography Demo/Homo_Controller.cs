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
using OpenCVForUnity.Calib3dModule;

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
public class Homo_Controller : MonoBehaviour
{
    public static double THRESH_VAL = 150.0;
    public static int K_ITERATIONS = 10;
    public static double HOMOGRAPHY_WIDTH = 640.0;
    public static double HOMOGRAPHY_HEIGHT = 480.0;

    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat inMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat erodeMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat dilMat = new Mat(480, 640, CvType.CV_8UC1);
    // private Mat homoMat = new Mat((int) HOMOGRAPHY_HEIGHT, (int) HOMOGRAPHY_WIDTH, CvType.CV_32FC1);
    // private Mat homoMat = new Mat(480, 640, CvType.CV_32FC1);
    private Mat homoMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);

    private float blob_x;
    private float blob_y;
    private float blob_r;

    private float ray_x;
    private float ray_y;
    private float ray_r;

    private ScreenOrientation? m_CachedOrientation = null;
    private Texture2D m_Texture;

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
    RawImage m_RawImage;
    public RawImage rawImage 
    {
        get { return m_RawImage; }
        set { m_RawImage = value; }
    }

    [SerializeField]
    Text m_ImageInfo;
    public Text imageInfo
    {
        get { return m_ImageInfo; }
        set { m_ImageInfo = value; }
    }

    [SerializeField]
    ARSessionOrigin m_ARSessionManager;
    public ARSessionOrigin sessionManager
    {
        get { return m_ARSessionManager; }
        set { m_ARSessionManager = value; }
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
        {
            m_ARCameraManager.frameReceived += OnCameraFrameReceived;
        }
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
        MatOfKeyPoint keyMat = new MatOfKeyPoint();
        inMat = imageMat;
        // inMat = (Mat.ones(imageMat.rows(), imageMat.cols(), CvType.CV_8UC1) * 255) - imageMat;

        // Creating Detector (Yellow Circle)
        // MatOfKeyPoint keyMat = new MatOfKeyPoint();
        // SimpleBlobDetector detector = SimpleBlobDetector.create();
        
        double[] homo_points = m_ARSessionManager.GetComponent<AR_Controller>().GetHomopoints();

        // Display Homography Points
        // outMat = inMat;
        // Imgproc.circle(outMat, new Point(homo_points[0], homo_points[1]), 5, new Scalar(0.0, 0.0, 255.0));
        // Imgproc.circle(outMat, new Point(homo_points[2], homo_points[3]), 5, new Scalar(0.0, 0.0, 255.0));
        // Imgproc.circle(outMat, new Point(homo_points[4], homo_points[5]), 5, new Scalar(0.0, 0.0, 255.0));
        // Imgproc.circle(outMat, new Point(homo_points[6], homo_points[7]), 5, new Scalar(0.0, 0.0, 255.0));

        // Creating MatOfPoint2f arrays for Homography Points
        Point[] srcPointArray = new Point[4];
        for (int i = 0; i < 4; i++)
        {
            srcPointArray[i] = new Point(homo_points[2*i], homo_points[(2*i)+1]);
        }

        Point[] dstPointArray = new Point[4];
        dstPointArray[0] = new Point(0.0, HOMOGRAPHY_HEIGHT);
        dstPointArray[1] = new Point(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT);
        dstPointArray[2] = new Point(0.0, 0.0);
        dstPointArray[3] = new Point(HOMOGRAPHY_WIDTH, 0.0);

        MatOfPoint2f srcPoints = new MatOfPoint2f(srcPointArray);
        MatOfPoint2f dstPoints = new MatOfPoint2f(dstPointArray);

        // Creating the H Matrix
        Mat Homo_Mat = Calib3d.findHomography(srcPoints, dstPoints);

        Debug.Log(Homo_Mat);
        Debug.Log(outMat.size());

        Imgproc.warpPerspective(inMat, outMat, Homo_Mat, new Size(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT));
    }

    void ConfigureRawImageInSpace(Vector2 img_dim)
    {
        Vector2 ScreenDimension = new Vector2(Screen.width, Screen.height);
        int scr_w = Screen.width;
        int scr_h = Screen.height; 

        float img_w = img_dim.x;
        float img_h = img_dim.y;

        float w_ratio = (float)scr_w/img_w;
        float h_ratio = (float)scr_h/img_h;
        float scale = Math.Max(w_ratio, h_ratio);

        Debug.LogFormat("Screen Dimensions: {0} x {1}\n Image Dimensions: {2} x {3}\n Ratios: {4}, {5}", 
            scr_w, scr_h, img_w, img_h, w_ratio, h_ratio);
        Debug.LogFormat("RawImage Rect: {0}", m_RawImage.uvRect);

        m_RawImage.SetNativeSize();
        m_RawImage.transform.position = new Vector3(scr_w/4, scr_h/4, 0.0f);
        m_RawImage.transform.localScale = new Vector3(scale/4, scale/4, 0.0f);
        // m_RawImage.transform.position = new Vector3(scr_w/2, scr_h/2, 0.0f);
        // m_RawImage.transform.localScale = new Vector3(scale, scale, 0.0f);
    }

    void FindRaycastPoint()
    {
        float w_ratio = (float)Screen.width/640;
        float h_ratio = (float)Screen.height/480;
        float scale = Math.Max(w_ratio, h_ratio);

        ray_x = scale * blob_x;
        ray_y = 1080.0f - (3.375f * (blob_y - 80.0f));
        ray_r = scale * blob_r;

        m_ImageInfo.text = string.Format("{0} x {1}", ray_x, ray_y);
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

        // Instantiates new m_Texture if necessary
        if (m_Texture == null || m_Texture.width != image.width)
        {
            var format = TextureFormat.RGBA32;
            m_Texture = new Texture2D(image.width, image.height, format, false);
        }

        image.Dispose();

        // Sets orientation of screen if necessary
        if (m_CachedOrientation == null || m_CachedOrientation != Screen.orientation)
        {
            // TODO: Debug why doesn't initiate with ConfigRawimage(). The null isn't triggering here. Print cached Orientation
            m_CachedOrientation = Screen.orientation;
            ConfigureRawImageInSpace(img_dim);
        }

        // Process the image here: 
        unsafe {
            IntPtr greyPtr = (IntPtr) greyscale.data.GetUnsafePtr();
            ComputerVisionAlgo(greyPtr);

            // Displays OpenCV Mat as a Texture
            Utils.matToTexture2D(outMat, m_Texture, true, 0);
        }

        m_RawImage.texture = (Texture) m_Texture;

        // Displays Raycast coordinates 
        // FindRaycastPoint();
    }
    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;

    ARSessionOrigin m_SessionOrigin;
}
