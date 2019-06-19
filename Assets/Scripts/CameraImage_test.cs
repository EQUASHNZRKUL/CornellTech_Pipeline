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
public class CameraImage_test : MonoBehaviour
{
    public Mat imageMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat edgeMat = new Mat(480, 640, CvType.CV_8UC1);
    public Mat outMat = new Mat(480, 640, CvType.CV_8UC1);

    public Mat circMat = new Mat(3, 5, CvType.CV_8UC1);
    private Size null_size = new Size(3.0, 5.0);
    // public Mat circMat = null;

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
        // Imgproc.threshold(imageMat, outMat, 128, 255, Imgproc.THRESH_BINARY_INV);

        Imgproc.Canny(imageMat, edgeMat, 90, 150);
        outMat = edgeMat;

        // Imgproc.HoughCircles(imageMat, circMat, Imgproc.HOUGH_GRADIENT, 1.0, 20.0);

        // Debug.LogFormat("circMat.at<int>(0, 0): {0}", circMat.ToString());
        // Debug.Log(circMat.size());

        // if (circMat.size() == null_size)
        // {
        //     Debug.Log("No circles found");
        // }
        // else
        // {
        //     Point center = Pooint
        // }

        // Debug.LogFormat("Circle 1: {0} x {1} -- {2}", 
        // circMat.get(0, 0)[0], circMat.get(0, 1)[0], circMat.get(0, 2)[0]);
        // for (int i = 0; i < 5; i++)
        // {
        //     Point center = Point(circMat[i][0], circMat[i][1]);
        //     int radius = circMat[i][2];
        //     circle(imageMat, center, 3, Scalar(0, 255, 0), -1, 8);
        //     circle(imageMat, center, radius, Scalar(0, 0, 255), 3, 8);
        // }

        // Debug.LogFormat("Mat Dimensions: {0} x {1}", imageMat.cols(), imageMat.rows());
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
        // m_RawImage.transform.localScale = new Vector3(w_ratio, h_ratio, 1.0f);
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

        // Debug.LogFormat("Dimensions: {0}\n\t Format: {1}\n\t Time: {2}\n\t ", 
        //     image.dimensions, image.format, image.timestamp);

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

        // Debug.LogFormat("Raw Image Coords: {0}\n Raw Image Scale: {1}", 
        //     m_RawImage.transform.position, m_RawImage.transform.localScale);

        m_RawImage.texture = (Texture) m_Texture;
        // Debug.Log(m_Texture.GetPixel(300, 300));
        // Debug.LogFormat("\n Texture Dimensions: {0} x {1}",
        //     m_Texture.width, m_Texture.height);
    }
}
