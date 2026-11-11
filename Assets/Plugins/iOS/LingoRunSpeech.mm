// LingoRun native speech plugin (iOS).
// Two features, one file:
//   * TTS via AVSpeechSynthesizer  — pronounce vocabulary words.
//   * Speech recognition via SFSpeechRecognizer + AVAudioEngine — the
//     pronunciation practice game.
// All results are pushed back to Unity with UnitySendMessage to a GameObject
// named "LingoRunSpeech" (created by NativeSpeech.cs at boot):
//   OnSpeechAuth    "1"/"0"    — mic + speech-recognition permission result
//   OnSpeechPartial "<text>"   — live transcription while listening
//   OnSpeechFinal   "<text>"   — final transcription for this listen
//   OnSpeechError   "<reason>" — recognizer unavailable / engine failure
//
// Requires Info.plist keys NSMicrophoneUsageDescription and
// NSSpeechRecognitionUsageDescription (added by Editor/IOSBuildSetup.cs).

#import <AVFoundation/AVFoundation.h>
#import <Speech/Speech.h>

static AVSpeechSynthesizer *lr_synth = nil;
static SFSpeechRecognizer *lr_recognizer = nil;
static SFSpeechAudioBufferRecognitionRequest *lr_request = nil;
static SFSpeechRecognitionTask *lr_task = nil;
static AVAudioEngine *lr_engine = nil;

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);

static void lr_send(const char *method, NSString *msg)
{
    UnitySendMessage("LingoRunSpeech", method, msg ? [msg UTF8String] : "");
}

extern "C" {

void _lingorunSpeak(const char *text, const char *locale, float rate)
{
    if (text == NULL) return;
    if (lr_synth == nil) lr_synth = [[AVSpeechSynthesizer alloc] init];
    [lr_synth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];

    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryPlayback
             withOptions:AVAudioSessionCategoryOptionMixWithOthers
                   error:nil];
    [session setActive:YES error:nil];

    AVSpeechUtterance *u = [AVSpeechUtterance speechUtteranceWithString:
                            [NSString stringWithUTF8String:text]];
    u.voice = [AVSpeechSynthesisVoice voiceWithLanguage:
               [NSString stringWithUTF8String:locale]];
    u.rate = rate;
    [lr_synth speakUtterance:u];
}

void _lingorunStopSpeaking()
{
    if (lr_synth != nil)
        [lr_synth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
}

void _lingorunRequestSpeechAuth()
{
    [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus status) {
        [[AVAudioSession sharedInstance] requestRecordPermission:^(BOOL granted) {
            dispatch_async(dispatch_get_main_queue(), ^{
                BOOL ok = (status == SFSpeechRecognizerAuthorizationStatusAuthorized) && granted;
                lr_send("OnSpeechAuth", ok ? @"1" : @"0");
            });
        }];
    }];
}

void _lingorunStopListening()
{
    if (lr_engine != nil && lr_engine.isRunning)
    {
        [lr_engine stop];
        [[lr_engine inputNode] removeTapOnBus:0];
    }
    if (lr_request != nil) [lr_request endAudio];
    if (lr_task != nil) { [lr_task cancel]; lr_task = nil; }
    lr_request = nil;
}

void _lingorunStartListening(const char *locale)
{
    _lingorunStopListening();

    NSLocale *loc = [NSLocale localeWithLocaleIdentifier:
                     [NSString stringWithUTF8String:locale]];
    lr_recognizer = [[SFSpeechRecognizer alloc] initWithLocale:loc];
    if (lr_recognizer == nil || !lr_recognizer.isAvailable)
    {
        lr_send("OnSpeechError", @"recognizer-unavailable");
        return;
    }

    NSError *err = nil;
    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryPlayAndRecord
             withOptions:AVAudioSessionCategoryOptionDefaultToSpeaker
                   error:&err];
    [session setActive:YES error:&err];

    if (lr_engine == nil) lr_engine = [[AVAudioEngine alloc] init];
    lr_request = [[SFSpeechAudioBufferRecognitionRequest alloc] init];
    lr_request.shouldReportPartialResults = YES;

    AVAudioInputNode *input = [lr_engine inputNode];
    AVAudioFormat *fmt = [input outputFormatForBus:0];
    SFSpeechAudioBufferRecognitionRequest *req = lr_request;
    [input installTapOnBus:0 bufferSize:1024 format:fmt
                     block:^(AVAudioPCMBuffer *buf, AVAudioTime *when) {
        [req appendAudioPCMBuffer:buf];
    }];

    [lr_engine prepare];
    if (![lr_engine startAndReturnError:&err])
    {
        lr_send("OnSpeechError", @"audio-engine-failed");
        return;
    }

    lr_task = [lr_recognizer recognitionTaskWithRequest:lr_request
                                          resultHandler:^(SFSpeechRecognitionResult *result, NSError *error) {
        if (result != nil)
        {
            NSString *text = result.bestTranscription.formattedString;
            BOOL final = result.isFinal;
            dispatch_async(dispatch_get_main_queue(), ^{
                lr_send(final ? "OnSpeechFinal" : "OnSpeechPartial", text);
            });
        }
        if (error != nil)
        {
            dispatch_async(dispatch_get_main_queue(), ^{
                lr_send("OnSpeechError", error.localizedDescription);
            });
        }
    }];
}

} // extern "C"
