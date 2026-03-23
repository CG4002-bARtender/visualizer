// HandTracker.mm
// Minimal native plugin: takes raw BGRA pixel data from Unity,
// runs VNDetectHumanHandPoseRequest, returns landmarks.
// No AVCaptureSession — uses ARKit's own camera frames passed from C#.

#import <Foundation/Foundation.h>
#import <Vision/Vision.h>
#import <CoreVideo/CoreVideo.h>

extern "C" {

/// Runs hand pose detection on raw BGRA pixel data.
/// Called from C# with pixel data from ARCameraManager's CPU image.
/// Writes 9 floats: wristX,Y, indexMCPX,Y, middleMCPX,Y, thumbTipX,Y, confidence
/// Returns 1 if hand detected, 0 otherwise.
int DetectHandPose(void *pixelData, int width, int height, int bytesPerRow,
                   int orientation, float *outData) {
    if (!pixelData || !outData || width <= 0 || height <= 0) return 0;

    // Wrap the raw BGRA pixel data in a CVPixelBuffer (no copy — zero overhead)
    CVPixelBufferRef pixelBuffer = NULL;
    CVReturn status = CVPixelBufferCreateWithBytes(
        kCFAllocatorDefault,
        (size_t)width, (size_t)height,
        kCVPixelFormatType_32BGRA,
        pixelData,
        (size_t)bytesPerRow,
        NULL, NULL, NULL,
        &pixelBuffer
    );

    if (status != kCVReturnSuccess || !pixelBuffer) {
        NSLog(@"[HandTracker] CVPixelBuffer creation failed: %d", status);
        return 0;
    }

    VNDetectHumanHandPoseRequest *request = [[VNDetectHumanHandPoseRequest alloc] init];
    request.maximumHandCount = 1;

    VNImageRequestHandler *handler = [[VNImageRequestHandler alloc]
        initWithCVPixelBuffer:pixelBuffer
                  orientation:(CGImagePropertyOrientation)orientation
                      options:@{}];

    NSError *error = nil;
    [handler performRequests:@[request] error:&error];
    CVPixelBufferRelease(pixelBuffer);

    if (error) {
        static int errCount = 0;
        if (++errCount <= 3) NSLog(@"[HandTracker] Vision error: %@", error.localizedDescription);
        return 0;
    }

    NSArray<VNHumanHandPoseObservation *> *observations = request.results;
    if (observations.count == 0) return 0;

    VNHumanHandPoseObservation *hand = observations.firstObject;

    VNRecognizedPoint *wrist = [hand recognizedPointForJointName:VNHumanHandPoseObservationJointNameWrist error:nil];
    VNRecognizedPoint *indexMCP = [hand recognizedPointForJointName:VNHumanHandPoseObservationJointNameIndexMCP error:nil];
    VNRecognizedPoint *middleMCP = [hand recognizedPointForJointName:VNHumanHandPoseObservationJointNameMiddleMCP error:nil];
    VNRecognizedPoint *thumbTip = [hand recognizedPointForJointName:VNHumanHandPoseObservationJointNameThumbTip error:nil];

    if (!wrist || wrist.confidence < 0.1f ||
        !indexMCP || indexMCP.confidence < 0.1f ||
        !middleMCP || middleMCP.confidence < 0.1f) {
        return 0;
    }

    outData[0] = (float)wrist.x;
    outData[1] = (float)wrist.y;
    outData[2] = (float)indexMCP.x;
    outData[3] = (float)indexMCP.y;
    outData[4] = (float)middleMCP.x;
    outData[5] = (float)middleMCP.y;
    outData[6] = thumbTip ? (float)thumbTip.x : 0.0f;
    outData[7] = thumbTip ? (float)thumbTip.y : 0.0f;
    outData[8] = hand.confidence;

    static int detections = 0;
    if (++detections <= 5 || detections % 60 == 0) {
        NSLog(@"[HandTracker] HAND #%d wrist=(%.2f,%.2f) conf=%.2f",
              detections, wrist.x, wrist.y, hand.confidence);
    }

    return 1;
}

}
