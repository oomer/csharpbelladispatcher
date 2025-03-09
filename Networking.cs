using NetMQ;
using NetMQ.Sockets;
using System.Security.Cryptography;

namespace oomer_imgui_learn3;

/// <summary>
/// Represents the state of a frame in the rendering pipeline.
/// </summary>
[Flags]
public enum Frame {
    /// <summary>Frame is available for processing</summary>
    Available = 0b_0000_0000,  // 0
    /// <summary>Frame processing has been completed</summary>
    Completed = 0b_0000_0001,  // 1
    /// <summary>Frame is currently being rendered</summary>
    Rendering = 0b_0000_0010,  // 2
    /// <summary>Frame data has been received</summary>
    Received =  0b_0000_0100   // 4
}

/// <summary>
/// Handles networking operations for distributed rendering tasks.
/// Manages communication with worker nodes for rendering jobs distribution and results collection.
/// </summary>
public class Networking {
    // Constants for network configuration
    private const string LOCALHOST = "tcp://localhost";
    private const int IMAGE_PORT = 8800;
    private const int COMMAND_PORT = 8799;
    private const int MAX_WORKERS = 10;
    private const int MAX_FRAMES = 10;

    // Synchronization object for thread safety
    private readonly object _syncLock = new object();
    
    // Core components
    private readonly Anim _anim;
    private readonly int _port;
    private readonly BellaPart _bella;
    
    // Worker tracking
    public bool[] _worker;
    public bool _renderfarm = false;
    
    // Worker state tracking collections
    public Dictionary<int, string> _workerDictionary = new Dictionary<int, string>();
    public Dictionary<int, long> _workerStart = new Dictionary<int, long>();
    public Dictionary<int, long> _workerEnd = new Dictionary<int, long>();

    /// <summary>
    /// Initializes a new instance of the Networking class.
    /// </summary>
    /// <param name="in_port">The port to use for network communication</param>
    /// <param name="fromanim">The animation data to use for rendering</param>
    /// <param name="inbella">The BellaPart instance to use for rendering configuration</param>
    public Networking(int in_port, Anim fromanim, BellaPart inbella) {
        _bella = inbella;
        _port = in_port;
        _anim = fromanim;
        
        // Initialize worker dictionaries
        for (int c = 0; c < MAX_WORKERS; c++) {
            _workerDictionary.Add(c, "not assigned");
            _workerStart.Add(c, 0);
            _workerEnd.Add(c, 0);
        }
    }

    /// <summary>
    /// Starts the image channel that receives rendered images from workers.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ImageChannelAsync() {
        await Task.Run(async () => { 
            try {
                using (var imageBroker = new ResponseSocket()) {
                    imageBroker.Bind($"{LOCALHOST}:{IMAGE_PORT}");
                    
                    int renderFrame = -1; // Note: This variable isn't updated in this method
                    
                    while (true) {
                        Console.WriteLine("imageLOOP");
                        
                        // Receive frame identifier and image data
                        string sframe = imageBroker.ReceiveFrameString();
                        byte[] png = imageBroker.ReceiveFrameBytes();
                        
                        // Save the received image
                        System.IO.File.WriteAllBytes(sframe + ".png", png);
                        imageBroker.SendFrame("ok");
                        
                        Console.WriteLine("imagecommand: " + sframe);
                        
                        // Record completion time
                        DateTime currentTime = DateTime.Now;
                        long epochTimeSeconds = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                        _workerEnd[renderFrame] = epochTimeSeconds;
                    }
                }
            } catch (NetMQException ex) {
                LogErrorAndExit("NetMQ Exception in ImageChannel", ex);
            } catch (Exception ex) {
                LogErrorAndExit("Exception in ImageChannel", ex);
            } 
        });
    }

    /// <summary>
    /// Starts the command channel that distributes rendering tasks to worker nodes.
    /// </summary>
    /// <param name="bellaFile">Path to the Bella scene file to be rendered</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CommandChannelAsync(string bellaFile) {
        await Task.Run(async () => { 
            try {
                using (var broker = new RouterSocket()) {
                    broker.Bind($"{LOCALHOST}:{COMMAND_PORT}");
                    
                    int renderFrame = -1;
                    byte[] identity;
                    string command;
                    string workerUuid;
                    
                    while (true) {
                        if (_renderfarm) {
                            // Since this is a RouterSocket, we expect uuid first
                            identity = broker.ReceiveFrameBytes();
                            command = broker.ReceiveFrameString();
                            workerUuid = new Guid(identity).ToString();
                            Console.WriteLine($"Worker::{workerUuid} {renderFrame}");

                            // Process different command types
                            switch (command) {
                                case "getBsz":
                                    HandleGetBszCommand(broker, identity, renderFrame, bellaFile);
                                    break;
                                    
                                case "readyImage":
                                    HandleReadyImageCommand(broker, identity, renderFrame);
                                    break;
                                    
                                case "getFragment":
                                    renderFrame = HandleGetFragmentCommand(broker, identity, renderFrame, workerUuid);
                                    break;
                                    
                                default:
                                    HandleDefaultCommand(broker, identity, renderFrame, bellaFile);
                                    break;
                            }
                        } else {
                            Thread.Sleep(2000);
                        }
                    }
                }
            } catch (NetMQException ex) {
                LogErrorAndExit("NetMQ Exception in CommandChannel", ex);
            } catch (Exception ex) {
                LogErrorAndExit("Exception in CommandChannel", ex);
            }
        });
    }

    /// <summary>
    /// Handles the "getBsz" command from workers requesting the Bella scene file.
    /// </summary>
    private void HandleGetBszCommand(RouterSocket broker, byte[] identity, int renderFrame, string bellaFile) {
        broker.SendMoreFrame(identity);
        broker.SendMoreFrame("");

        if (renderFrame >= MAX_FRAMES) {
            broker.SendFrame("standby");
        } else {
            broker.SendMoreFrame("Sending .bsz");
            byte[] byteFile = System.IO.File.ReadAllBytes(bellaFile);
            broker.SendFrame(byteFile);
        }
    }

    /// <summary>
    /// Handles the "readyImage" command from workers indicating they're ready to send rendered images.
    /// </summary>
    private void HandleReadyImageCommand(RouterSocket broker, byte[] identity, int renderFrame) {
        Console.WriteLine("readyimage");
        broker.SendMoreFrame(identity);
        broker.SendMoreFrame("");
        broker.SendFrame(renderFrame.ToString());
        Console.WriteLine("PPPPNNGG");
    }

    /// <summary>
    /// Handles the "getFragment" command from workers requesting rendering work.
    /// </summary>
    /// <returns>The updated render frame index</returns>
    private int HandleGetFragmentCommand(RouterSocket broker, byte[] identity, int renderFrame, string workerUuid) {
        broker.SendMoreFrame(identity);
        broker.SendMoreFrame("");
        
        // Move to the next frame
        renderFrame += 1;
        
        if (renderFrame > MAX_FRAMES) {
            broker.SendFrame("standby");
        } else {
            string transformData = PrepareRenderingData(renderFrame, workerUuid);
            
            Console.WriteLine("getFragment" + renderFrame);
            broker.SendMoreFrame(renderFrame.ToString());
            broker.SendMoreFrame(transformData);
            broker.SendFrame("ee: " + renderFrame);
        }
        
        return renderFrame;
    }

    /// <summary>
    /// Prepares rendering data for a specific frame.
    /// </summary>
    /// <returns>Camera transform data as a string</returns>
    private string PrepareRenderingData(int renderFrame, string workerUuid) {
        string transformData;
        
        lock (_syncLock) {
            // Calculate camera animation for the current frame
            (bella.Mat4 animMat4, double lerpFocus) = _anim.slerpCamera((float)(renderFrame * 3) / _anim._totalFrames);
            
            // Debug output
            Console.WriteLine(animMat4.ToString());
            Console.WriteLine(lerpFocus.ToString());
            Console.WriteLine("animMat4");
            
            // Update camera transform and focus
            _bella._camXform["steps"][0]["xform"].set(animMat4);
            transformData = _bella._camXform["steps"][0]["xform"].valueToString();
            _bella._camLens["steps"][0]["FocusDist"].set(lerpFocus);
            Console.WriteLine("animMat4");
            
            // Record worker assignment and start time
            _workerDictionary[renderFrame] = workerUuid;
            DateTime currentTime = DateTime.Now;
            long epochTimeSeconds = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            _workerStart[renderFrame] = epochTimeSeconds;
        }
        
        return transformData;
    }

    /// <summary>
    /// Handles default command case (file checksum request).
    /// </summary>
    private void HandleDefaultCommand(RouterSocket broker, byte[] identity, int renderFrame, string bellaFile) {
        broker.SendMoreFrame(identity);
        broker.SendMoreFrame("");
        
        if (renderFrame >= MAX_FRAMES) {
            broker.SendFrame("standby");
        } else {
            // Calculate and send SHA256 checksum of the bella file
            using (FileStream stream = File.OpenRead(bellaFile)) {
                SHA256Managed sha = new SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                string sha256sum = BitConverter.ToString(checksum).Replace("-", String.Empty);
                broker.SendFrame(sha256sum);
            }
        }
    }

    /// <summary>
    /// Logs an error message and exits the application.
    /// </summary>
    /// <param name="message">The error context message</param>
    /// <param name="ex">The exception that occurred</param>
    private void LogErrorAndExit(string message, Exception ex) {
        Console.WriteLine($"{message}: {ex.Message}");
        Environment.Exit(1);
    }
}







