using System.Drawing;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using bella;
using libBella = bella.bella;
using System.Numerics;

namespace oomer_imgui_learn3;
public class GUI {
    ImGuiController? _imguiController;
    IInputContext? _inputContext;
    private static IWindow? _silkWindow;
    public static GL? _openglContext;
    private static uint _glTextureID;
    private readonly object _synclock = new object();
    private bool _orbiting  = false;
    private bool _zooming  = false;
    private bool _panning  = false;
    private double _speed   = 0.5;
    private System.Numerics.Vector2 _mousePos = new System.Numerics.Vector2(0,0);
    private BellaPart _bella;
    public Anim _anim;
    public Networking _networking;
    private bella.Mat4[] _camAnim = new bella.Mat4[2];
    private double[] _focusDistAnim = new double[2];
    private bella.Node _camXform;
    private bella.Input _camLens;
    private uint _step = 0;
    private bool _isAnimRunning = false;


    string _bellaFile;
    int _port;

    bool[] _frames = Enumerable.Repeat(false, 100).ToArray();

    public GUI( string in_bellaFile, int in_port ) {
        _bellaFile = in_bellaFile;
        _port = in_port;
    }
    public void Start() {
        _anim = new Anim();
        _bella = new BellaPart();
        _networking = new Networking( _port, _anim, _bella );
        _networking.CommandChannelAsync( _bellaFile );
        _networking.ImageChannelAsync( );
        WindowStart();

    }
    public async Task WindowStart() {
        // Create a Silk.NET window as usual
        WindowOptions options = WindowOptions.Default with {
            Size = new Silk.NET.Maths.Vector2D<int>( 512, 512 ),
            Title = "Oomer Bella Dispatcher"
        };
        _silkWindow = Window.Create( options );
        
        // loading function
        _silkWindow.Load += OnWindowLoad;

        // Handle resizes
        //_silkWindow.FramebufferResize += newSize => {
        //    _openglContext?.Viewport( newSize );
        //};

        // render function
        _silkWindow.Render += OnWindowRender; 
        _silkWindow.Update += OnWindowUpdate; 

        // closing function
        _silkWindow.Closing += OnWindowClose;
        _silkWindow.Run();
        _silkWindow.Dispose();
    }
    private void OnWindowClose() {
        _imguiController?.Dispose();
        _inputContext?.Dispose();
        _openglContext?.Dispose();
        _bella.BellaClose();
    }
    private void OnWindowLoad() {
        //_bella = new BellaPart();
        _bella.BellaInit( _bellaFile );

        // Save camera keyframes
        //var camPath =_bella._engine.scene().world().firstPathTo( _bella._engine.scene().camera() ); 
        //var camPathlist = camPath.nodes();
        _camXform = _bella._engine.scene().cameraPath().parent();
        //_camXform = camPathlist[camPathlist.Count-2];
        _anim._camAnim[0] = _camXform["steps"][0]["xform"].asMat4();
        _anim._camAnim[1] = _camXform["steps"][0]["xform"].asMat4();
        _camLens = _bella._engine.scene().camera()["lens"];
        _anim._focusDistAnim[0] = _camLens["steps"][0]["FocusDist"].asReal();
        _anim._focusDistAnim[1] = _camLens["steps"][0]["FocusDist"].asReal();

        _imguiController = new ImGuiController(
            _openglContext = _silkWindow.CreateOpenGL(), 
            _silkWindow, 
            _inputContext = _silkWindow?.CreateInput()
        );

        // Capture all input devices
        for ( int i = 0; i < _inputContext?.Keyboards.Count; i++ ) {
            _inputContext.Keyboards[ i ].KeyDown += KeyDown;
        }
        for ( int i = 0; i < _inputContext?.Keyboards.Count; i++ ) {
            _inputContext.Keyboards[ i ].KeyUp += KeyUp;
        }

        for ( int i = 0; i < _inputContext?.Mice.Count; i++ ) {
            _inputContext.Mice[ i ].MouseDown += MouseDown;
        }
        for ( int i = 0; i < _inputContext?.Mice.Count; i++ ) {
            _inputContext.Mice[ i ].MouseUp += MouseUp;
        }
        for ( int i = 0; i < _inputContext?.Mice.Count; i++ ) {
            _inputContext.Mice[ i ].MouseMove += MouseMove;
        }
        // OpenGL is a state machine
        _openglContext.TexParameterI( GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat );
        _openglContext.TexParameterI( GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat );
        _openglContext.TexParameterI( GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest );
        _openglContext.TexParameterI( GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest );
        // Bind texture
        _openglContext.BindTexture( TextureTarget.Texture2D, 0 );
    }
    private unsafe void OnWindowRender( double deltaTime ) {
        _imguiController?.Update( (float) deltaTime );
        _openglContext?.ClearColor(    Color.FromArgb(255, 
                                        (int)(.45f * 255), 
                                        (int) (.55f * 255), 
                                        (int) (.60f * 255) ) );
        _openglContext?.Clear( ClearBufferMask.ColorBufferBit );
        ImGuiNET.ImGui.SetCursorPos( new System.Numerics.Vector2( 0,0 ) );
        ImGuiNET.ImGui.ShowStyleEditor();

        ImGuiNET.ImGui.PushStyleVar( ImGuiNET.ImGuiStyleVar.WindowBorderSize,0 );
        ImGuiNET.ImGui.PushStyleVar( ImGuiNET.ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2( 0,0 ) );
        ImGuiNET.ImGui.Begin(   "Bella", 
                                ImGuiNET.ImGuiWindowFlags.NoCollapse | 
                                ImGuiNET.ImGuiWindowFlags.NoMove | 
                                ImGuiNET.ImGuiWindowFlags.NoTitleBar );  
        ImGuiNET.ImGui.SetCursorPos( new System.Numerics.Vector2( 0,0 ) );
        float rezratio = (float)_bella._width/(float)_bella._height;
        int newheight = (int)(( 400*(float)_bella._height ) / (float)_bella._width);

        //ImGuiNET.ImGui.ShowDemoWindow();
        //`ImGuiNET.ImGui.Text( newheight.ToString() );
        if (!_networking._renderfarm) {
            ImGuiNET.ImGui.Image( (nint)_glTextureID, 
                                    new System.Numerics.Vector2( 
                                        400 , 
                                        newheight
                                    ) );

            var mousePos = ImGuiNET.ImGui.GetMousePos();
            ImGuiNET.ImGui.Text( mousePos.X.ToString() + "," + mousePos.Y.ToString() );
            ImGuiNET.ImGui.Text( "keyframe "+_step.ToString() );
            if (_networking._worker == null) {
            } else {
                ImGuiNET.ImGui.Text( "workers"+_networking._worker.Length);
            }
            
            //ImGuiNET.ImGui.PushStyleVar(ImGuiNET.ImGuiStyleVar.Alpha, ImGuiNET.ImGui.GetStyle().Alpha * 0.5f);

            if ( _isAnimRunning) {
                if ( ImGuiNET.ImGui.Button( "Stop" )) {
                    _isAnimRunning = false;
                    _step = 0;
                    JumpKey(_step);
                }
            } else {
                if ( ImGuiNET.ImGui.Button( "Play" )) {
                    _isAnimRunning = true;
                    _step = 0;
                }
            }
            ImGuiNET.ImGui.SameLine();
            if ( ImGuiNET.ImGui.Button( "KeyFrame 0" )) {
                JumpKey(0);
            }
            ImGuiNET.ImGui.SameLine();
            if ( ImGuiNET.ImGui.Button( "KeyFrame 1" )) {
                JumpKey(1);
            }
            if ( ImGuiNET.ImGui.Button( "Send to renderfarm" )) {
                if ( _networking._renderfarm ) {
                    _networking._renderfarm = false;
                } else {
                    _networking._renderfarm = true;
                }
            }
        } else {
            ImGuiNET.ImGui.Text( "job is rendering" );
            for (int c=0; c<10; c++) {
                //ImGuiNET.ImGui.Text( c + ": " + _networking._workuuids[c] );
                ImGuiNET.ImGui.Text( c + ": " + _networking._workerDictionary[c] );
                // Convert epoch time to a DateTime object
                //DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds( _networking._workerTime[c]).UtcDateTime;

                // Convert DateTime to a human-readable string
                //string readableTimeString = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime currentTime = DateTime.Now;
                long epochTimeSecondsNow = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                /*
                if ( _networking._workerEnd[c]  == 0 ) {
                    lock(synclock) {
                        _networking._workerEnd[c] = epochTimeSecondsNow;
                    }
                }*/
                _networking._workerEnd[c] = epochTimeSecondsNow;
                ImGuiNET.ImGui.Text( c + ": " + ( _networking._workerEnd[c] - _networking._workerStart[c]).ToString() );
                
            }
        }
            
        // Draw bella ipr window by passing GL Texture ID
        ImGuiNET.ImGui.End();
        _imguiController?.Render();
    }
    private void OnWindowUpdate( double deltaTime ) {
        if ( _isAnimRunning) {
            _anim._currentFrame += 1;
            if (_anim._currentFrame > _anim._totalFrames) { 
                _anim._currentFrame = 0; 
                _isAnimRunning = false;
                _step = 1;
                JumpKey(1);
                Thread.Sleep(100);
            } else {
                (bella.Mat4 animMat4, double lerpFocus) = _anim.slerpCamera( (float) _anim._currentFrame/_anim._totalFrames);
                _camXform["steps"][0]["xform"].set( animMat4 );
                _camLens["steps"][0]["FocusDist"].set( lerpFocus );
                Console.WriteLine( _camXform["steps"][0]["xform"].valueToString());
                Thread.Sleep(500);
            }
        }
        if ( _bella._bellaWindowUpdate == true ) {
            lock( _synclock ) {
                unsafe {
                    IntPtr pixelsAddr = _bella._skBitmap.GetPixels();
                    // Cast IntPtr addres to a pointer
                    // this also works: byte* ptr = (byte*)pixelsAddr;
                    byte* ptr = (byte*) pixelsAddr.ToPointer(); {
                        _openglContext?.TexImage2D( TextureTarget.Texture2D,
                        0, 
                        InternalFormat.Rgba, 
                        (uint)_bella._skBitmap.Width, 
                        (uint)_bella._skBitmap.Height, 
                        0, 
                        PixelFormat.Rgba, 
                        PixelType.UnsignedByte, 
                        ptr );
                    }
                }
            }
            _bella._bellaWindowUpdate = false;
        }
    }
    private void KeyDown( IKeyboard keyboard, Key key, int keyCode ) {
        if ( key == Key.Escape ) { _silkWindow?.Close(); }
        if ( key == Key.ControlLeft ) {
            _zooming = true;
            _mousePos = _inputContext.Mice[0].Position;
        }
        if ( key == Key.Space ) {
            _panning = true;
            _mousePos = _inputContext.Mice[0].Position;
        }
    }
    private void KeyUp( IKeyboard keyboard, Key key, int keyCode ) {
        if ( key == Key.ControlLeft ) { 
            _zooming = false; 
            _anim._focusDistAnim[ _step ] = _camLens["steps"][0]["FocusDist"].asReal();
        }
        if ( key == Key.Space ) { _panning = false; }
    }
    public void MouseUp( IMouse mouse, MouseButton but ) {
        _orbiting = false;
        _mousePos = new System.Numerics.Vector2( 0,0 );
    }
    public void MouseDown( IMouse mouse, MouseButton but ) {
        //if ( mouse.Position.X <= _imWinWidth && mouse.Position.Y <= _imWinHeight ) {
        _orbiting = true;
        _mousePos = mouse.Position;
        //}
    }
    public void MouseMove( IMouse mouse, System.Numerics.Vector2 pos ) {
        if ( _orbiting ) {
            _isAnimRunning = false;
            var delta = new Vec2(( pos[0] - _mousePos.X) * _speed, ( pos[1] - _mousePos.Y ) * _speed);
            bella.Mat4 camMat4 = libBella.orbitCamera( _bella._engine?.scene().cameraPath(), delta );
            _anim._camAnim[_step] = camMat4;
            _mousePos = pos;
        } else if ( _zooming ) {
            _isAnimRunning = false;
            var delta = new Vec2(( pos[0] - _mousePos.X) * _speed, ( pos[1] - _mousePos.Y ) * _speed);
            _anim._camAnim[ _step ] = libBella.zoomCamera( _bella._engine?.scene().cameraPath(), delta );
            _mousePos = pos;
        } else if ( _panning ) {
            _isAnimRunning = false;
            var delta = new Vec2(( pos[0] - _mousePos.X) * .01, ( pos[1] - _mousePos.Y ) * .01);
            _anim._camAnim[_step] = libBella.panCamera( _bella._engine?.scene().cameraPath(), delta, true );
            _mousePos = pos;
        }
    }
    public void JumpKey( uint key1 ) {
        _isAnimRunning = false;
        _camXform["steps"][0]["xform"].set(_anim._camAnim[key1]);
        _camLens["steps"][0]["FocusDist"].set(_anim._focusDistAnim[key1]);
        _step = key1;
    }
}
