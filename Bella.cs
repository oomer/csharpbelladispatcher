using SkiaSharp;
using System;

namespace oomer_imgui_learn3;

/// <summary>
/// Implementation of the IBella interface that manages interactions with the Bella rendering engine.
/// Handles rendering operations, scene management, and image processing.
/// </summary>
public class BellaPart : IBella {
	// Constants
	private const int DEFAULT_MIN_RESOLUTION = 480;

	// Engine components
	private EngineObserver? _engineObserver;
	public bella.Engine? _engine;
	
	// Synchronization and state tracking
	private readonly object _syncLock = new object();
	public SKBitmap _skBitmap;
	public bool _bellaWindowUpdate = false;
	public float _progress;
	
	// Image dimensions
	public int _width;
	public int _height;
	
	// Camera controls
	public bella.Node _camXform;
	public bella.Input _camLens;

	/// <summary>
	/// Initializes the Bella rendering engine and loads the specified scene.
	/// </summary>
	/// <param name="bellaFilePath">Path to the Bella scene file (.bsz, .bsa, or .bsx)</param>
	/// <exception cref="ArgumentNullException">Thrown if the file path is null</exception>
	/// <exception cref="FileNotFoundException">Thrown if the specified file does not exist</exception>
	public void BellaInit(string bellaFilePath) {
		if (string.IsNullOrEmpty(bellaFilePath)) {
			throw new ArgumentNullException(nameof(bellaFilePath), "Bella scene file path cannot be null or empty");
		}
		
		if (!File.Exists(bellaFilePath)) {
			throw new FileNotFoundException("Bella scene file not found", bellaFilePath);
		}
		
		try {
			_engine = new bella.Engine();
			_engineObserver = new EngineObserver(this);
			
			var scenePath = Path.GetFullPath(bellaFilePath);
			_engine.subscribe(_engineObserver);
			_engine.scene().loadDefs();
			_engine.stop();
			_engine.loadScene(scenePath);
		}
		catch (Exception ex) {
			throw new InvalidOperationException($"Failed to initialize Bella engine: {ex.Message}", ex);
		}
	}

	/// <summary>
	/// Processes and displays a rendered image from the Bella engine.
	/// Converts the Bella image format to SKBitmap for display.
	/// </summary>
	/// <param name="pass">The render pass that produced the image</param>
	/// <param name="img">The image data from the renderer</param>
	public void ShowImage(string pass, bella.Image img) {
		lock (_syncLock) {
			unsafe {
				// Copy pixel data from Bella image to SKBitmap
				IntPtr pixelsAddr = _skBitmap.GetPixels();
				var width = (int)img.width();
				var height = (int)img.height();
				
				byte* destPtr = (byte*)pixelsAddr.ToPointer();
				byte* srcPtr = (byte*)img.rgba8().ToPointer();
				
				// Manual pixel-by-pixel copy with RGBA components
				for (int row = 0; row < height; row++) {
					for (int col = 0; col < width; col++) {
						*destPtr++ = *srcPtr++; // Red
						*destPtr++ = *srcPtr++; // Green
						*destPtr++ = *srcPtr++; // Blue
						*destPtr++ = *srcPtr++; // Alpha
					}
				}
			}
		}
		
		// Signal that the window needs updating
		_bellaWindowUpdate = true;
	}

	/// <summary>
	/// Handles scene loaded notification from the Bella engine.
	/// Configures rendering parameters and initializes the bitmap for display.
	/// </summary>
	/// <param name="scene">The loaded scene data</param>
	public void onSceneLoaded(bella.Scene scene) {
		Console.WriteLine("Scene loaded successfully");
		
		try {
			// Get effective resolution based on camera settings
			bella.Vec2u effectiveResolution = scene.resolution(scene.camera(), true, true);
			
			// Configure engine for interactive mode
			_engine.enableInteractiveMode();
			_engine.scene().beautyPass()["targetNoise"] = 10;
			
			// Set dimensions based on resolution
			SetDimensions(effectiveResolution);
			
			// Get camera references for animation
			_camXform = _engine.scene().cameraPath().parent();
			_camLens = _engine.scene().camera()["lens"].asNode();
			
			// Create bitmap for display
			_skBitmap = new SKBitmap((int)effectiveResolution.x, (int)effectiveResolution.y);
			
			// Start rendering
			_engine.start();
		}
		catch (Exception ex) {
			Console.WriteLine($"Error during scene setup: {ex.Message}");
		}
	}

	/// <summary>
	/// Sets the display dimensions based on the effective resolution.
	/// </summary>
	/// <param name="resolution">The effective resolution from the scene</param>
	private void SetDimensions(bella.Vec2u resolution) {
		_width = (int)resolution.x;
		_height = (int)resolution.y;
	}

	/// <summary>
	/// Handles error messages from the Bella engine.
	/// </summary>
	/// <param name="pass">The render pass where the error occurred</param>
	/// <param name="msg">The error message</param>
	public void onError(string pass, string msg) {
		Console.WriteLine($"ERROR in {pass}: {msg}");
	}

	/// <summary>
	/// Handles progress updates from the Bella engine.
	/// </summary>
	/// <param name="pass">The render pass currently in progress</param>
	/// <param name="progress">Progress information from the renderer</param>
	public void onProgress(string pass, bella.Progress progress) {
		_progress = (float)progress.progress();
	}

	/// <summary>
	/// Releases resources and closes the Bella engine.
	/// </summary>
	public void BellaClose() {
		try {
			if (_engine != null) {
				_engine.Dispose();
				_engine = null;
			}
			
			if (_engineObserver != null) {
				_engineObserver.Dispose();
				_engineObserver = null;
			}
		}
		catch (Exception ex) {
			Console.WriteLine($"Error during Bella cleanup: {ex.Message}");
		}
	}
}
