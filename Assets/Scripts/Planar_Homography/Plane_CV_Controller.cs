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

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
public class Plane_CV_Controller : MonoBehaviour
{
    public static double THRESH_VAL = 150.0;
    public static int K_ITERATIONS = 10;
    public static double HOMOGRAPHY_WIDTH = 640.0;
    public static double HOMOGRAPHY_HEIGHT = 480.0;

    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    private Mat inMat = new Mat(480, 640, CvType.CV_8UC1); 
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);

    private Mat cached_initMat = new Mat (480, 640, CvType.CV_8UC1);
    private Mat cached_homoMat = new Mat (480, 640, CvType.CV_8UC1);

    private MatOfKeyPoint keyMat = new MatOfKeyPoint();
    private Point[] srcPointArray = new Point[4];
    private Point[] regPointArray = new Point[4];
    private Point[] dstPointArray = new Point[4];

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

    public Point[] GetC1Points()
    {
        return srcPointArray;
    }

    // private Point[] getCornerPoints()
    // {
    //     if (keyMat.size().width < 4)
    //         return new Point[0]; 

    //     double[] point1 = keyMat.get(0,0);
    //     double[] point2 = keyMat.get(1,0);
    //     double[] point3 = keyMat.get(2,0);
    //     double[] point4 = keyMat.get(3,0);

    //     Point[] ret = new Point[4];
    //     ret[0] = new Point(point1[0], point1[1]);
    //     ret[1] = new Point(point2[0], point2[1]);
    //     ret[2] = new Point(point3[0], point3[1]);
    //     ret[3] = new Point(point4[0], point4[1]);

    //     return ret;
    // }

    void CornerDetection() {
        // Creating Detector
        int octaves = 6;
        float corner_thresh = 0.015f;
        float dog_thresh = 0.015f;
        int max_detections = 5;
        HarrisLaplaceFeatureDetector detector = HarrisLaplaceFeatureDetector.create(
            octaves, corner_thresh, dog_thresh, max_detections);

        // Finding corners
        // imageMat = cached_initMat;
        Core.flip(cached_initMat, imageMat, 0);
        keyMat = new MatOfKeyPoint();
        detector.detect(imageMat, keyMat);

        // Draw corners
        Features2d.drawKeypoints(imageMat, keyMat, outMat);
    }

    void SortPoints() {
        Point storeGreaterY(Point fst, Point snd) {
            if (fst.y > snd.y)
                return fst; 
            return snd; 
        }

        // Find top points
        Point one = new Point(0.0, 0.0);
        Point two = new Point(0.0, 0.0);
        int i_1 = 0;
        int i_2 = 0;
        for (int i = 0; i < 4; i++) {
            one = storeGreaterY(one, srcPointArray[i]);
            i_1 = i; 
        }
        for (int i = 0; i < 4; i++) {
            if (srcPointArray[i] != one) {
                two = storeGreaterY(two, srcPointArray[i]);
                i_2 = i;
            }
        }
        if (one.x > two.x) { // Swap if necessary
            Point tmp = one; 
            one = two; 
            two = tmp; 
        }

        // Find low points
        Point three = new Point(0.0, 0.0);
        Point four = new Point(0.0, 0.0);
        for (int i = 0; i < 4; i++) {
            if ((srcPointArray[i] != one) && (srcPointArray[i] != two)) {
                if (three == four) { // TODO; replace with == new point(0.0, 0.0)
                    three = srcPointArray[i];
                }
                else {
                    four = srcPointArray[i];
                }
            }
        }
        if (three.x > four.x) { // Swap if necessary
            Point tmp = three; 
            three = four; 
            four = tmp; 
        }

        // storing sorted values
        srcPointArray[0] = one;
        srcPointArray[1] = two; 
        srcPointArray[2] = three; 
        srcPointArray[3] = four; 
    }

    void BlobDetection() {
        SimpleBlobDetector detector = SimpleBlobDetector.create();
        // inMat = imageMat; 

        Core.flip(cached_initMat, imageMat, 0);

        keyMat = new MatOfKeyPoint();
        detector.detect(imageMat, keyMat);

        // Features2d.drawKeypoints(imageMat, keyMat, outMat);

        if (keyMat.rows() < 4) 
            return; 

        for (int i = 0; i < 4; i++)
        {
            srcPointArray[i] = new Point(keyMat.get(i, 0)[0], keyMat.get(i, 0)[1]);
        }
        
        SortPoints();

        regPointArray[0] = new Point(0.0, HOMOGRAPHY_HEIGHT);
        regPointArray[1] = new Point(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT);
        regPointArray[2] = new Point(0.0, 0.0);
        regPointArray[3] = new Point(HOMOGRAPHY_WIDTH, 0.0);

        MatOfPoint2f srcPoints = new MatOfPoint2f(srcPointArray);
        MatOfPoint2f regPoints = new MatOfPoint2f(regPointArray);

        // Creating the H Matrix
        Mat Homo_Mat = Calib3d.findHomography(srcPoints, regPoints);

        Imgproc.warpPerspective(imageMat, cached_homoMat, Homo_Mat, new Size(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT));
    }

    void HomographyTransform(IntPtr greyscale) 
    {
        Plane_AR_Controller Homo_Controller = m_ARSessionManager.GetComponent<Plane_AR_Controller>();
        // Point[] c1_scrpoints = Homo_Controller.GetScreenpoints(true);
        // Point[] c2_scrpoints = Homo_Controller.GetScreenpoints(false);
        // TODO: get new c2_scrpoints; 

        MatOfPoint2f initPoints = new MatOfPoint2f(regPointArray);
        // MatOfPoint2f initPoints = new MatOfPoint2f(c1_scrpoints);
        MatOfPoint2f currPoints = new MatOfPoint2f(c2_scrpoints);

        print(c2_scrpoints[0]);

        // ISSUE IS INITPOINTS, CURRPOINTS. 

        Mat H = Calib3d.findHomography(initPoints, currPoints);

        Imgproc.warpPerspective(cached_homoMat, outMat, H, new Size(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT));
        // Imgproc.warpPerspective(cached_homoMat, outMat, H, new Size(HOMOGRAPHY_WIDTH, HOMOGRAPHY_HEIGHT));
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

            // TOUCH: Cache image on touch
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    // Cache original image
                    Utils.copyToMat(greyPtr, cached_initMat);

                    // Detect reference points
                    BlobDetection();
                    Debug.Log(keyMat.size());
                
                }
            }

            // Try ignoring Homography code and displaying detected corners
            HomographyTransform(greyPtr);

            // Displays OpenCV Mat as a Texture
            Utils.matToTexture2D(outMat, m_Texture, false, 0);
        }

        // Sets orientation of screen if necessary
        if (m_CachedOrientation == null || m_CachedOrientation != Screen.orientation)
        {
            // TODO: Debug why doesn't initiate with ConfigRawimage(). The null isn't triggering here. Print cached Orientation
            m_CachedOrientation = Screen.orientation;
            ConfigureRawImageInSpace(img_dim);
        }

        m_RawImage.texture = (Texture) m_Texture;

        m_ImageInfo.text = string.Format("Number of Blobs: {0}", keyMat.rows());
    }

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    ARRaycastManager m_ARRaycastManager;

    ARSessionOrigin m_SessionOrigin;
}