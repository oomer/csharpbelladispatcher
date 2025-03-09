using NetMQ;
using NetMQ.Sockets;
using System.Security.Cryptography;

namespace oomer_imgui_learn3;

[Flags]
public enum Frame {
    Available = 0b_0000_0000,  // 0
    Completed = 0b_0000_0001,  // 1
    Rendering = 0b_0000_0010,  // 2
    Received =  0b_0000_0100  // 4
}

public class Networking {
    private readonly object synclock = new object();
    private Anim _anim;
    private int _port;
    public bool[] _worker;
    private BellaPart _bella;
    public bool _renderfarm = false;
    public Dictionary<int,string> _workerDictionary = new Dictionary<int, string>() ;
    public Dictionary<int,long> _workerStart = new Dictionary<int, long>() ;
    public Dictionary<int,long> _workerEnd = new Dictionary<int, long>() ;

    public Networking( int in_port, Anim fromanim, BellaPart inbella ){
        _bella = inbella;
        _port = in_port;
        _anim  = fromanim;
        for (int c =0 ; c < 10;  c++) {
            _workerDictionary.Add(c,"not assigned");
            _workerStart.Add(c,0);
            _workerEnd.Add(c,0);
        }
    }
    public async Task ImageChannelAsync( ) {
        await Task.Run( async () => { 
            // Without this await this task would run synchronously
            try {
                using ( var imageBroker = new ResponseSocket()) {
                    imageBroker.Bind( "tcp://localhost:8800" );
                    
                    int renderFrame = -1; 
                    byte[] identity;
                    string command;
                    string workeruuid;
                    while ( true ) {
                        Console.WriteLine("imageLOOP");
                            string sframe = imageBroker.ReceiveFrameString();
                            byte[] png = imageBroker.ReceiveFrameBytes();
                            System.IO.File.WriteAllBytes( sframe+".png", png ); 
                            imageBroker.SendFrame("ok");
                            Console.WriteLine("imagecommand: "+sframe);
                            DateTime currentTime = DateTime.Now;
                            long epochTimeSeconds = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                            _workerEnd[renderFrame] = epochTimeSeconds;
                    }
                }
            } catch ( NetMQException ex ) {
                Console.WriteLine("NetMQ Exception: " + ex.Message);       
                Environment.Exit(1);
            } catch ( Exception ex) {
                Console.WriteLine("Exception: " + ex.Message);       
                Environment.Exit(1);

            } 
        });
    }
    public async Task CommandChannelAsync( string bellaFile ) {
        await Task.Run( async () => { 
            // Without this await this task would run synchronously
            try {
                using ( var broker = new RouterSocket()) {
                    broker.Bind( "tcp://localhost:8799" );
                    int renderFrame = -1; 
                    byte[] identity;
                    string command;
                    string workeruuid;
                    while ( true ) {
                        if ( _renderfarm ) {
                            // Since this is a RouterSocket, we expect uuid 
                            identity = broker.ReceiveFrameBytes();
                            command = broker.ReceiveFrameString();
                            workeruuid = new Guid(identity).ToString();
                            Console.WriteLine("Worker::"+workeruuid+" "+renderFrame);

                            if ( command == "getBsz" ) {
                                //Sequence is identity, then null string and then data
                                broker.SendMoreFrame( identity );
                                broker.SendMoreFrame( "" );

                                if (renderFrame >= 10) { broker.SendFrame( "standby");
                                } else {
                                    broker.SendMoreFrame( "Sending .bsz" );
                                    byte[] byteFile = System.IO.File.ReadAllBytes( bellaFile );
                                    broker.SendFrame( byteFile );
                                }
                            } else if ( command == "readyImage" ) {
                                Console.WriteLine( "readyimage" );
                                broker.SendMoreFrame( identity );
                                broker.SendMoreFrame( "" );
                                broker.SendFrame( renderFrame.ToString() );
                                Console.WriteLine("PPPPNNGG");

                            } else if ( command == "getFragment" ) {
                                broker.SendMoreFrame( identity );
                                broker.SendMoreFrame( "" );
                                renderFrame += 1;
                                if (renderFrame > 10) { broker.SendFrame( "standby");
                                } else {
                                    string bsa1;
                                    lock (synclock) {
                                        (bella.Mat4 animMat4, double lerpFocus) = _anim.slerpCamera( (float) (renderFrame*3)/_anim._totalFrames);
                                        System.Numerics.Matrix4x4 ddd = new System.Numerics.Matrix4x4();
                                        ddd = System.Numerics.Matrix4x4.Identity;
                                        Console.WriteLine(animMat4.ToString());
                                        Console.WriteLine(lerpFocus.ToString());
                                        Console.WriteLine("animMat4");
                                        _bella._camXform["steps"][0]["xform"].set( animMat4 );
                                        bsa1 = _bella._camXform["steps"][0]["xform"].valueToString();
                                        _bella._camLens["steps"][0]["FocusDist"].set( lerpFocus );
                                        Console.WriteLine("animMat4");
                                        _workerDictionary[renderFrame] = workeruuid;
                                        DateTime currentTime = DateTime.Now;
                                        long epochTimeSeconds = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                                        _workerStart[renderFrame] = epochTimeSeconds;
                                    }

                                    Console.WriteLine("getFragment"+renderFrame);
                                    broker.SendMoreFrame( renderFrame.ToString() );
                                    broker.SendMoreFrame( bsa1 );
                                    broker.SendFrame( "ee: "+renderFrame );
                                }
                            } else {
                                broker.SendMoreFrame( identity );
                                broker.SendMoreFrame( "" );
                                if (renderFrame >= 10) { broker.SendFrame( "standby");
                                } else {
                                    using (FileStream stream = File.OpenRead(bellaFile))
                                    {
                                        SHA256Managed sha = new SHA256Managed();
                                        byte[] checksum = sha.ComputeHash(stream);
                                        string sha256sum = BitConverter.ToString(checksum).Replace("-", String.Empty);
                                        broker.SendFrame( sha256sum );
                                    }
                                }
                            }
                        } else {
                            Thread.Sleep(2000);
                        }
                    }
                }
            } catch ( NetMQException ex ) {
                Console.WriteLine("NetMQ Exception: " + ex.Message);       
                Environment.Exit(1);
            } catch ( Exception ex) {
                Console.WriteLine("Exception: " + ex.Message);       
                Environment.Exit(1);

            }
        });
    }
}







