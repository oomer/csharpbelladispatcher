using System.Drawing;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using bella;
using libBella = bella.bella;
using System.Numerics;

namespace oomer_imgui_learn3;

/// <summary>
/// Manages the GUI interface for the Bella renderer application.
/// Handles window creation, rendering, user input, and interactions with the rendering engine.
/// </summary>
public class GUI {
    // Constants
    private const float DEFAULT_ANIMATION_FRAME_DELAY_MS = 500;
    private const float DEFAULT_CAMERA_SPEED = 0.5f;
    private const float DEFAULT_PANNING_FACTOR = 0.01f;
    private const int DEFAULT_DISPLAY_WIDTH = 400;
    private const int MAX_FRAMES = 100;
    
    // ImGui and window components
    private ImGuiController? _imguiController;
    private IInputContext? _inputContext;
    private static IWindow? _silkWindow;
    public static GL? _openglContext;
    private static uint _glTextureID;
    
    // Synchronization
    private readonly object _syncLock = new object();
    
    // Camera control state
    private bool _orbiting = false;
    private bool _zooming = false;
    private bool _panning = false;
    private double _speed = DEFAULT_CAMERA_SPEED;
    private Vector2 _mousePos = new Vector2(0, 0);
    
    // Bella renderer components
    private BellaPart _bella;
    public Anim _anim;
    public Networking _networking;
    
    // Animation state
    private bella.Mat4[] _camAnim = new bella.Mat4[2];
    private double[] _focusDistAnim = new double[2];
    private bella.Node _camXform;
    private bella.Input _camLens;
    private uint _step = 0;
    private bool _isAnimRunning = false;
    
    // File info
    private string _bellaFile;
    private int _port;
    
    // Render frame tracking
    private bool[] _frames = Enumerable.Repeat(false, MAX_FRAMES).ToArray();

    /// <summary>
    /// Initializes a new instance of the GUI class.
    /// </summary>
    /// <param name="in_bellaFile">Path to the Bella scene file</param>
    /// <param name="in_port">Network port for communication with worker nodes</param>
    public GUI(string in_bellaFile, int in_port) {
        _bellaFile = in_bellaFile ?? throw new ArgumentNullException(nameof(in_bellaFile));
        _port = in_port;
    }
    
    /// <summary>
    /// Starts the application by initializing the Bella renderer, networking, and window.
    /// </summary>
    public void Start() {
        try {
            // Initialize core components
            _anim = new Anim();
            _bella = new BellaPart();
            _networking = new Networking(_port, _anim, _bella);
            
            // Start networking channels
            _networking.CommandChannelAsync(_bellaFile);
            _networking.ImageChannelAsync();
            
            // Start window
            WindowStart();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error starting application: {ex.Message}");
            Environment.Exit(1);
        }
    }
    
    /// <summary>
    /// Initializes and starts the application window.
    /// </summary>
    public async Task WindowStart() {
        try {
            // Create a Silk.NET window
            WindowOptions options = WindowOptions.Default with {
                Size = new Silk.NET.Maths.Vector2D<int>(512, 512),
                Title = "Oomer Bella Dispatcher"
            };
            
            _silkWindow = Window.Create(options);
            
            // Register window event handlers
            _silkWindow.Load += OnWindowLoad;
            _silkWindow.Render += OnWindowRender;
            _silkWindow.Update += OnWindowUpdate;
            _silkWindow.Closing += OnWindowClose;
            
            // Start window
            _silkWindow.Run();
            _silkWindow.Dispose();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error creating window: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Releases resources when the window is closing.
    /// </summary>
    private void OnWindowClose() {
        try {
            _imguiController?.Dispose();
            _inputContext?.Dispose();
            _openglContext?.Dispose();
            _bella.BellaClose();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Initializes resources when the window loads.
    /// </summary>
    private void OnWindowLoad() {
        try {
            // Initialize Bella renderer
            _bella.BellaInit(_bellaFile);
            
            // Save initial camera keyframes
            InitializeCameraKeyframes();
            
            // Initialize ImGui controller
            _imguiController = new ImGuiController(
                _openglContext = _silkWindow.CreateOpenGL(),
                _silkWindow,
                _inputContext = _silkWindow?.CreateInput()
            );
            
            // Register input event handlers
            SetupInputHandlers();
            
            // Configure OpenGL texture parameters
            ConfigureOpenGLTexture();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error during window load: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Initializes camera keyframes for animation.
    /// </summary>
    private void InitializeCameraKeyframes() {
        _camXform = _bella._engine.scene().cameraPath().parent();
        _anim._camAnim[0] = _camXform["steps"][0]["xform"].asMat4();
        _anim._camAnim[1] = _camXform["steps"][0]["xform"].asMat4();
        
        _camLens = _bella._engine.scene().camera()["lens"];
        _anim._focusDistAnim[0] = _camLens["steps"][0]["FocusDist"].asReal();
        _anim._focusDistAnim[1] = _camLens["steps"][0]["FocusDist"].asReal();
    }
    
    /// <summary>
    /// Sets up input event handlers for keyboard and mouse.
    /// </summary>
    private void SetupInputHandlers() {
        if (_inputContext == null) return;
        
        // Register keyboard event handlers
        for (int i = 0; i < _inputContext.Keyboards.Count; i++) {
            _inputContext.Keyboards[i].KeyDown += KeyDown;
            _inputContext.Keyboards[i].KeyUp += KeyUp;
        }
        
        // Register mouse event handlers
        for (int i = 0; i < _inputContext.Mice.Count; i++) {
            _inputContext.Mice[i].MouseDown += MouseDown;
            _inputContext.Mice[i].MouseUp += MouseUp;
            _inputContext.Mice[i].MouseMove += MouseMove;
        }
    }
    
    /// <summary>
    /// Configures OpenGL texture parameters.
    /// </summary>
    private void ConfigureOpenGLTexture() {
        if (_openglContext == null) return;
        
        // Set texture parameters
        _openglContext.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
        _openglContext.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
        _openglContext.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _openglContext.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
        
        // Bind texture
        _openglContext.BindTexture(TextureTarget.Texture2D, 0);
    }
    
    /// <summary>
    /// Renders the window content.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last frame</param>
    private unsafe void OnWindowRender(double deltaTime) {
        try {
            // Update ImGui controller
            _imguiController?.Update((float)deltaTime);
            
            // Clear the background
            SetBackgroundColor();
            
            // Start ImGui rendering
            ConfigureImGuiWindowStyle();
            
            // Render main window
            RenderMainWindow();
            
            // Finish ImGui rendering
            _imguiController?.Render();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error during rendering: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Sets the background color and clears the buffer.
    /// </summary>
    private void SetBackgroundColor() {
        if (_openglContext == null) return;
        
        _openglContext.ClearColor(
            Color.FromArgb(255, 
                (int)(.45f * 255), 
                (int)(.55f * 255), 
                (int)(.60f * 255)
            )
        );
        _openglContext.Clear(ClearBufferMask.ColorBufferBit);
    }
    
    /// <summary>
    /// Configures ImGui window style settings.
    /// </summary>
    private void ConfigureImGuiWindowStyle() {
        ImGuiNET.ImGui.SetCursorPos(new Vector2(0, 0));
        ImGuiNET.ImGui.ShowStyleEditor();
        
        ImGuiNET.ImGui.PushStyleVar(ImGuiNET.ImGuiStyleVar.WindowBorderSize, 0);
        ImGuiNET.ImGui.PushStyleVar(ImGuiNET.ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
    }
    
    /// <summary>
    /// Renders the main application window with the Bella renderer output.
    /// </summary>
    private void RenderMainWindow() {
        ImGuiNET.ImGui.Begin(
            "Bella", 
            ImGuiNET.ImGuiWindowFlags.NoCollapse | 
            ImGuiNET.ImGuiWindowFlags.NoMove | 
            ImGuiNET.ImGuiWindowFlags.NoTitleBar
        );
        
        ImGuiNET.ImGui.SetCursorPos(new Vector2(0, 0));
        
        // Calculate aspect ratio and display size
        float aspectRatio = (float)_bella._width / (float)_bella._height;
        int displayHeight = (int)((DEFAULT_DISPLAY_WIDTH * (float)_bella._height) / (float)_bella._width);
        
        if (!_networking._renderfarm) {
            RenderPreviewMode(displayHeight);
        } else {
            RenderFarmMode();
        }
        
        ImGuiNET.ImGui.End();
    }
    
    /// <summary>
    /// Renders the UI for preview mode (local rendering).
    /// </summary>
    /// <param name="displayHeight">Height of the display area</param>
    private void RenderPreviewMode(int displayHeight) {
        // Display the rendered image
        ImGuiNET.ImGui.Image(
            (nint)_glTextureID,
            new Vector2(DEFAULT_DISPLAY_WIDTH, displayHeight)
        );
        
        // Show mouse position and other info
        Vector2 mousePos = ImGuiNET.ImGui.GetMousePos();
        ImGuiNET.ImGui.Text($"{mousePos.X},{mousePos.Y}");
        ImGuiNET.ImGui.Text($"keyframe {_step}");
        
        if (_networking._worker != null) {
            ImGuiNET.ImGui.Text($"workers {_networking._worker.Length}");
        }
        
        // Render control buttons
        RenderControlButtons();
    }
    
    /// <summary>
    /// Renders the control buttons for animation and render farm management.
    /// </summary>
    private void RenderControlButtons() {
        // Play/Stop button
        if (_isAnimRunning) {
            if (ImGuiNET.ImGui.Button("Stop")) {
                _isAnimRunning = false;
                _step = 0;
                JumpKey(_step);
            }
        } else {
            if (ImGuiNET.ImGui.Button("Play")) {
                _isAnimRunning = true;
                _step = 0;
            }
        }
        
        // Keyframe navigation buttons
        ImGuiNET.ImGui.SameLine();
        if (ImGuiNET.ImGui.Button("KeyFrame 0")) {
            JumpKey(0);
        }
        
        ImGuiNET.ImGui.SameLine();
        if (ImGuiNET.ImGui.Button("KeyFrame 1")) {
            JumpKey(1);
        }
        
        // Render farm toggle
        if (ImGuiNET.ImGui.Button("Send to renderfarm")) {
            _networking._renderfarm = !_networking._renderfarm;
        }
    }
    
    /// <summary>
    /// Renders the UI for render farm mode (distributed rendering).
    /// </summary>
    private void RenderFarmMode() {
        ImGuiNET.ImGui.Text("job is rendering");
        
        // Display worker status information
        for (int c = 0; c < 10; c++) {
            ImGuiNET.ImGui.Text($"{c}: {_networking._workerDictionary[c]}");
            
            // Calculate and display rendering time
            DateTime currentTime = DateTime.Now;
            long epochTimeSecondsNow = (long)(currentTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            _networking._workerEnd[c] = epochTimeSecondsNow;
            
            ImGuiNET.ImGui.Text($"{c}: {(_networking._workerEnd[c] - _networking._workerStart[c])}");
        }
    }
    
    /// <summary>
    /// Updates the application state.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since the last frame</param>
    private void OnWindowUpdate(double deltaTime) {
        try {
            // Update animation if running
            UpdateAnimation();
            
            // Update the display texture if needed
            UpdateDisplayTexture();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error during update: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Updates the animation state if the animation is running.
    /// </summary>
    private void UpdateAnimation() {
        if (!_isAnimRunning) return;
        
        // Advance animation frame
        _anim._currentFrame += 1;
        
        // Check if animation is complete
        if (_anim._currentFrame > _anim._totalFrames) {
            CompleteAnimation();
        } else {
            AdvanceAnimation();
        }
    }
    
    /// <summary>
    /// Completes the animation and resets to the final keyframe.
    /// </summary>
    private void CompleteAnimation() {
        _anim._currentFrame = 0;
        _isAnimRunning = false;
        _step = 1;
        JumpKey(1);
        Thread.Sleep(100);
    }
    
    /// <summary>
    /// Advances the animation to the current frame.
    /// </summary>
    private void AdvanceAnimation() {
        // Calculate interpolated camera transform and focus
        (bella.Mat4 animMat4, double lerpFocus) = _anim.slerpCamera((float)_anim._currentFrame / _anim._totalFrames);
        
        // Apply to the camera
        _camXform["steps"][0]["xform"].set(animMat4);
        _camLens["steps"][0]["FocusDist"].set(lerpFocus);
        
        Console.WriteLine(_camXform["steps"][0]["xform"].valueToString());
        
        // Add delay between frames
        Thread.Sleep((int)DEFAULT_ANIMATION_FRAME_DELAY_MS);
    }
    
    /// <summary>
    /// Updates the display texture with the latest rendered image.
    /// </summary>
    private unsafe void UpdateDisplayTexture() {
        if (!_bella._bellaWindowUpdate) return;
        
        lock (_syncLock) {
            IntPtr pixelsAddr = _bella._skBitmap.GetPixels();
            
            // Update OpenGL texture with the latest image data
            byte* ptr = (byte*)pixelsAddr.ToPointer();
            _openglContext?.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba,
                (uint)_bella._skBitmap.Width,
                (uint)_bella._skBitmap.Height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                ptr
            );
        }
        
        _bella._bellaWindowUpdate = false;
    }
    
    /// <summary>
    /// Handles keyboard key down events.
    /// </summary>
    /// <param name="keyboard">Keyboard device generating the event</param>
    /// <param name="key">Key that was pressed</param>
    /// <param name="keyCode">Key code</param>
    private void KeyDown(IKeyboard keyboard, Key key, int keyCode) {
        switch (key) {
            case Key.Escape:
                _silkWindow?.Close();
                break;
            
            case Key.ControlLeft:
                _zooming = true;
                _mousePos = _inputContext.Mice[0].Position;
                break;
            
            case Key.Space:
                _panning = true;
                _mousePos = _inputContext.Mice[0].Position;
                break;
        }
    }
    
    /// <summary>
    /// Handles keyboard key up events.
    /// </summary>
    /// <param name="keyboard">Keyboard device generating the event</param>
    /// <param name="key">Key that was released</param>
    /// <param name="keyCode">Key code</param>
    private void KeyUp(IKeyboard keyboard, Key key, int keyCode) {
        switch (key) {
            case Key.ControlLeft:
                _zooming = false;
                _anim._focusDistAnim[_step] = _camLens["steps"][0]["FocusDist"].asReal();
                break;
            
            case Key.Space:
                _panning = false;
                break;
        }
    }
    
    /// <summary>
    /// Handles mouse button up events.
    /// </summary>
    /// <param name="mouse">Mouse device generating the event</param>
    /// <param name="button">Mouse button that was released</param>
    public void MouseUp(IMouse mouse, MouseButton button) {
        _orbiting = false;
        _mousePos = new Vector2(0, 0);
    }
    
    /// <summary>
    /// Handles mouse button down events.
    /// </summary>
    /// <param name="mouse">Mouse device generating the event</param>
    /// <param name="button">Mouse button that was pressed</param>
    public void MouseDown(IMouse mouse, MouseButton button) {
        _orbiting = true;
        _mousePos = mouse.Position;
    }
    
    /// <summary>
    /// Handles mouse move events.
    /// </summary>
    /// <param name="mouse">Mouse device generating the event</param>
    /// <param name="position">New mouse position</param>
    public void MouseMove(IMouse mouse, Vector2 position) {
        if (_orbiting) {
            HandleOrbiting(position);
        } else if (_zooming) {
            HandleZooming(position);
        } else if (_panning) {
            HandlePanning(position);
        }
    }
    
    /// <summary>
    /// Handles camera orbiting based on mouse movement.
    /// </summary>
    /// <param name="position">Current mouse position</param>
    private void HandleOrbiting(Vector2 position) {
        _isAnimRunning = false;
        var delta = new Vec2((position.X - _mousePos.X) * _speed, (position.Y - _mousePos.Y) * _speed);
        bella.Mat4 camMat4 = libBella.orbitCamera(_bella._engine?.scene().cameraPath(), delta);
        _anim._camAnim[_step] = camMat4;
        _mousePos = position;
    }
    
    /// <summary>
    /// Handles camera zooming based on mouse movement.
    /// </summary>
    /// <param name="position">Current mouse position</param>
    private void HandleZooming(Vector2 position) {
        _isAnimRunning = false;
        var delta = new Vec2((position.X - _mousePos.X) * _speed, (position.Y - _mousePos.Y) * _speed);
        _anim._camAnim[_step] = libBella.zoomCamera(_bella._engine?.scene().cameraPath(), delta);
        _mousePos = position;
    }
    
    /// <summary>
    /// Handles camera panning based on mouse movement.
    /// </summary>
    /// <param name="position">Current mouse position</param>
    private void HandlePanning(Vector2 position) {
        _isAnimRunning = false;
        var delta = new Vec2((position.X - _mousePos.X) * DEFAULT_PANNING_FACTOR, (position.Y - _mousePos.Y) * DEFAULT_PANNING_FACTOR);
        _anim._camAnim[_step] = libBella.panCamera(_bella._engine?.scene().cameraPath(), delta, true);
        _mousePos = position;
    }
    
    /// <summary>
    /// Jumps to a specific keyframe.
    /// </summary>
    /// <param name="keyframeIndex">Index of the keyframe to jump to</param>
    public void JumpKey(uint keyframeIndex) {
        _isAnimRunning = false;
        _camXform["steps"][0]["xform"].set(_anim._camAnim[keyframeIndex]);
        _camLens["steps"][0]["FocusDist"].set(_anim._focusDistAnim[keyframeIndex]);
        _step = keyframeIndex;
    }
}
