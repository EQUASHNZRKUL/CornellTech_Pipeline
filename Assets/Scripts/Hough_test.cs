using System;
// using System.Collections.Generic;
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

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
// [RequireComponent(typeof(ARCameraManager))]
// [RequireComponent(typeof(RawImage))]
public class Hough_test : MonoBehaviour
{
    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat edgeMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat circMat = new Mat(3, 5, CvType.CV_8UC1);
    private Size null_size = new Size(3.0, 5.0);
    private double DP_CONST = 2.0;

    public Texture2D m_Texture;

    private ScreenOrientation? m_CachedOrientation = null;

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

    void Awake()
    {
        Debug.Log("StartTest");
        // m_ARCameraManager = GetComponent<ARCameraManager>();
        // m_RawImage = GetComponent<RawImage>();
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

        outMat = imageMat;

        // Imgproc.Canny(imageMat, edgeMat, 90, 150);
        Imgproc.HoughCircles(imageMat, circMat, Imgproc.HOUGH_GRADIENT, DP_CONST, 100.0);

        if (circMat.size() == null_size)
        {
            Debug.Log("No circles found");
        }
        else
        {
            double[] c_data = circMat.get(0, 0);
            Imgproc.circle(outMat, new Point(c_data[0], c_data[1]), (int) c_data[2], new Scalar(0.0, 0.0, 255.0));
        }
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
        m_RawImage.transform.position = new Vector3(scr_w/2, scr_h/2, 0.0f);
        m_RawImage.transform.localScale = new Vector3(scale, scale, 0.0f);
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // CAMERA IMAGE HANDLING
        XRCameraImage image;
        if (!cameraManager.TryGetLatestImage(out image))
        {
            Debug.Log("Uh Oh");
            return;
        }

        Vector2 img_dim = image.dimensions;
        
        XRCameraImagePlane greyscale = image.GetPlane(0);

        if (m_Texture == null || m_Texture.width != image.width || m_Texture.height != image.height)
        {
            var format = TextureFormat.RGBA32;
            m_Texture = new Texture2D(image.width, image.height, format, false);
        }

        image.Dispose();

        // Process the image here: 
        unsafe {
            IntPtr greyPtr = (IntPtr) greyscale.data.GetUnsafePtr();
            ComputerVisionAlgo(greyPtr);
            Utils.matToTexture2D(outMat, m_Texture, true, 0);
        }

        if (m_CachedOrientation == null || m_CachedOrientation != Screen.orientation)
        {
            m_CachedOrientation = Screen.orientation;
            ConfigureRawImageInSpace(img_dim);
        }

        m_RawImage.texture = (Texture) m_Texture;

        double[] c_data = circMat.get(0, 0);
        m_ImageInfo.text = string.Format("Circle Count: {0}\n Circle[0]: {1} x {2} -- {3}", 
        circMat.size().width, c_data[0], c_data[1], c_data[2]);
    }
}
