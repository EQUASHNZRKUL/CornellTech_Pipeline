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
using OpenCVForUnity.Xfeatures2dModule;

using OpenCVForUnity.ArucoModule;

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
public class Detector_CV_Controller : MonoBehaviour
{
    public static double THRESH_VAL = 150.0;
    public static int K_ITERATIONS = 10;
    public static double HOMOGRAPHY_WIDTH = 640.0;
    public static double HOMOGRAPHY_HEIGHT = 480.0;

    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat cached_initMat = new Mat (480, 640, CvType.CV_8UC1);
    private List<Mat> corners = new List<Mat>();
    private Mat ids = new Mat(480, 640, CvType.CV_8UC1);

    public bool spa_full = false; 
    private Point[] src_point_array = new Point[7];

    private ScreenOrientation? m_CachedOrientation = null;
    private Texture2D m_Texture;

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

    void Awake()
    {
        Debug.Log("StartTest");
        Screen.autorotateToLandscapeLeft = true; 
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

    float CameraToPixelX(double x)
    {
        return (float) (3.4375 * x);
    }

    float CameraToPixelY(double y)
    {
        return (float) (1080.0 - (3.375*(y - 80.0)));
        // return (float) (1080.0 - (1080.0/320.0)*(y-80.0));
    }

    int arucoTosrc(int a) {
        if (a == 7) { return 4; }
        else if (a == 6) { return 5; }
        else if (a == 10) { return 6; }
        else {return a; }
    }

    int srcToarcuo(int s) {
        if (s == 4) { return 7; }
        else if (s == 5) {return 6; }
        else if (s == 6) {return 10; }
        else {return s; }
    }

    int count_src_nulls() {
        int acc = 0;
        for (int i = 0; i < 7; i++)
        {
            if (src_point_array[i] == null) {
                acc++; 
            }
        }
        return (7 - acc); 
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

    void ArucoDetection() {
        Dictionary dict = Aruco.getPredefinedDictionary(Aruco.DICT_4X4_1000);
        Aruco.detectMarkers(cached_initMat, dict, corners, ids);
        Aruco.drawDetectedMarkers(cached_initMat, corners, ids);

            // Debug.LogFormat("{0}, {1}", corners[0].get(0,0)[0], corners[0].get(0,0)[1]); 
            // Debug.LogFormat("{0}, {1}", corners[0].get(0,1)[0], corners[0].get(0,1)[1]); 
            // Debug.LogFormat("{0}, {1}", corners[0].get(0,2)[0], corners[0].get(0,2)[1]); 
            // Debug.LogFormat("{0}, {1}", corners[0].get(0,3)[0], corners[0].get(0,3)[1]); 

        for (int i = 0; i < corners.Count; i++) {
            int aruco_id = (int) (ids.get(i, 0)[0]);
            int src_i = arucoTosrc(aruco_id);
            int corner_i = 3 - (aruco_id % 4);

            // Store corner[i] into spa[src_i]
            src_point_array[src_i] = new Point(corners[i].get(0, corner_i)[0], corners[i].get(0, corner_i)[1]);

            // Display the corner as circle on outMat. 
            Imgproc.circle(cached_initMat, src_point_array[src_i], 10, new Scalar(255, 255, 0));
        }

        // Count non-null source points 
        int markerCount = count_src_nulls();
        Debug.LogFormat("markerCount = {0}", markerCount);
        m_ImageInfo.text = string.Format("Number of markers detected: {0}", markerCount);
        spa_full = (markerCount == 7);

        Core.flip(cached_initMat, outMat, 0);
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

        // Process the image here: 
        unsafe {
            IntPtr greyPtr = (IntPtr) greyscale.data.GetUnsafePtr();

            // TOUCH: Detect corners and set as source points
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    // Cache original image
                    Utils.copyToMat(greyPtr, cached_initMat);

                    // Detect reference points
                    ArucoDetection();
                }
            }

            // Displays OpenCV Mat as a Texture
            Utils.matToTexture2D(outMat, m_Texture, false, 0);
        }

        if (spa_full) {
            // Homography shit
            // Debug.Log(" ALL SRC FOUND");
        }

        // Sets orientation of screen if necessary
        if (m_CachedOrientation == null || m_CachedOrientation != Screen.orientation)
        {
            // TODO: Debug why doesn't initiate with ConfigRawimage(). The null isn't triggering here. Print cached Orientation
            m_CachedOrientation = Screen.orientation;
            ConfigureRawImageInSpace(img_dim);
        }

        m_RawImage.texture = (Texture) m_Texture;

        // m_ImageInfo.text = string.Format("Number of Blobs: {0}", ids.size());
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;

    ARSessionOrigin m_SessionOrigin;
}