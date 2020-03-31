using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.Networking;
using System.Text;

public class PhoneCamera : MonoBehaviour
{
    private bool camAvailable;
    private WebCamTexture webCam;
    private Texture texture; // in case camera does not open

    public RawImage rawImage;
    public AspectRatioFitter fitter;
    public Text textOCR;
    void Start()
    {
        texture = rawImage.texture; // whatever image is in the scene view, that will be the defaultBackground
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.Log("Phone does not have a camera");
            camAvailable = false;
            return;
        }

        for (int i = 0; i < devices.Length; i++)
        {
            if (!devices[i].isFrontFacing)
            {
                webCam = new WebCamTexture(devices[i].name, Screen.width, Screen.height);
                break;
            }
        }

        if (webCam == null)
        {
            Debug.Log("No back facing camera found");
            return;
        }

        webCam.Play(); // we can start rendering
        rawImage.texture = webCam; // to be used as a normal texture

        camAvailable = true;
    }

    void Update()
    {
        if (!camAvailable)
        {
            // camera still not available
            return;
        }

        float ratio = (float)webCam.width / (float)webCam.height;
        fitter.aspectRatio = ratio;

        float scaleY = webCam.videoVerticallyMirrored ? -1f : 1f;
        rawImage.rectTransform.localScale = new Vector3(1f, scaleY, 1f); // flip in the Y axes if backCam is mirrored vertically

        int orient = -webCam.videoRotationAngle;
        rawImage.rectTransform.localEulerAngles = new Vector3(0, 0, orient);
    }
    public void btnClick()
    {
        byte[] jpg = getBytesFromImage();

        string encode = Convert.ToBase64String(jpg);

        StartCoroutine(requestVisionAPI(encode));
    }

    private byte[] getBytesFromImage()
    {
        Texture2D snap = new Texture2D(webCam.width, webCam.height);
        snap.SetPixels(webCam.GetPixels());
        snap.Apply();

        webCam.Pause();
        camAvailable = false;

        byte[] bytes = snap.EncodeToJPG();
        textOCR.text = "Byte length: " + bytes.Length;
        return bytes;
    }

    private IEnumerator requestVisionAPI(string base64Image)
    {
        string apiKey = "your key";
        string url = "https://vision.googleapis.com/v1/images:annotate?key=" + apiKey;

        // requestBodyを作成
        var requests = new requestBody();
        requests.requests = new List<AnnotateImageRequest>();

        var request = new AnnotateImageRequest();
        request.image = new Image();
        request.image.content = base64Image;

        request.features = new List<Feature>();
        var feature = new Feature();
        feature.type = FeatureType.TEXT_DETECTION.ToString();
        feature.maxResults = 10;
        request.features.Add(feature);

        requests.requests.Add(request);

        // JSON
        string jsonRequestBody = JsonUtility.ToJson(requests);

        // "application/json"
        var webRequest = new UnityWebRequest(url, "POST");
        byte[] postData = Encoding.UTF8.GetBytes(jsonRequestBody);
        webRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(postData);
        webRequest.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        yield return webRequest.SendWebRequest();

        // when google cloud vision responds
        if (webRequest.isNetworkError)
        {
            Debug.Log("Error: " + webRequest.error);
        }
        else
        {
            var responses = JsonUtility.FromJson<responseOCR>(webRequest.downloadHandler.text);

            string data = System.Text.Encoding.UTF8.GetString(webRequest.downloadHandler.data);

            Debug.Log("Response: " + data);

            String textInImage = string.Empty;
            if (responses.responses.Count() == 1)
            {
                if (responses.responses.First().fullTextAnnotation.text != null)
                {
                    textInImage = responses.responses.First().fullTextAnnotation.text;
                    
                }
            }

            textOCR.text = textInImage;
        }
    }

    [Serializable]
    public class requestBody
    {
        public List<AnnotateImageRequest> requests;
    }

    [Serializable]
    public class AnnotateImageRequest
    {
        public Image image;
        public List<Feature> features;
        //public string imageContext;
    }

    [Serializable]
    public class Image
    {
        public string content;
        //public ImageSource source;
    }

    [Serializable]
    public class Feature
    {
        public string type;
        public int maxResults;
    }

    public enum FeatureType
    {
        TYPE_UNSPECIFIED,
        FACE_DETECTION,
        LANDMARK_DETECTION,
        LOGO_DETECTION,
        LABEL_DETECTION,
        TEXT_DETECTION,
        SAFE_SEARCH_DETECTION,
        IMAGE_PROPERTIES
    }

    [Serializable]
    public class ImageContext
    {
        public LatLongRect latLongRect;
        public string languageHints;
    }

    [Serializable]
    public class LatLongRect
    {
        public LatLng minLatLng;
        public LatLng maxLatLng;
    }

    [Serializable]
    public class LatLng
    {
        public float latitude;
        public float longitude;
    }

    public class responseOCR
    {
        public List<AnnotateImageOCR_Response> responses;
    }

    [Serializable]
    public class AnnotateImageOCR_Response
    {
        public List<EntityTextAnnotation> textAnnotations;
        public EntityTextAnnotation fullTextAnnotation;
    }

    [Serializable]
    public class EntityTextAnnotation
    {
        public string mid;
        public string locale; // used in OCR
        public string description; // used in OCR
        public float score;
        public float confidence;
        public float topicality;
        public BoundingPoly boundingPoly; // used in OCR
        public List<LocationInfo> locations;
        public List<Property> properties; 
        public string text; // used in OCR
    }

    [Serializable]
    public class BoundingPoly
    {
        public List<Vertex> vertices;
    }

    [Serializable]
    public class Vertex
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class LocationInfo
    {
        LatLng latLng;
    }

    [Serializable]
    public class Property
    {
        string name;
        string value;
    }
}




