using SkiaSharp;

namespace oomer_imgui_learn3;
public class BellaPart : IBella {
	private EngineObserver? _engineObserver;
	public bella.Engine? _engine;
    private readonly object _synclock = new object();
    public SKBitmap _skBitmap;
    public bool _bellaWindowUpdate = false;
    public int _width;
    public int _height;

    public bella.Node _camXform;
    public bella.Input _camLens;

    public float _progress;

    public void BellaInit( string in_bellaFile ) {
        _engine = new bella.Engine();
        _engineObserver = new EngineObserver(this);
        var scenePath = System.IO.Path.GetFullPath( in_bellaFile );
        _engine.subscribe(_engineObserver);
        _engine.scene().loadDefs();
        _engine.stop();
        _engine.loadScene(scenePath);
    }
    public void ShowImage( string pass, bella.Image img ) {
        lock( _synclock ) {
            unsafe {
                //Console.WriteLine("si {0} {1}",img.width(),img.height());
                IntPtr pixelsAddr = _skBitmap.GetPixels();
                var w = (int)img.width();
                var h = (int)img.height();
                byte* destptr = (byte*)pixelsAddr.ToPointer();
                byte* srcptr = (byte*)img.rgba8().ToPointer();
                for ( int row = 0; row < h; row++ ) {
                    for ( int col = 0; col < w; col++ ) {
                        *destptr++ = *srcptr++; //red Using ++ on pointer advances to next pointer
                        *destptr++ = *srcptr++; //green
                        *destptr++ = *srcptr++; //blue
                        *destptr++ = *srcptr++; //alpha
                    }
                }
            }
        }
        _bellaWindowUpdate = true;
    }

    public void onSceneLoaded( bella.Scene scene ) {
        Console.WriteLine("onSceneLoaded");
        bella.Vec2u effres = scene.resolution(scene.camera(),true,true);
        _engine.enableInteractiveMode();
        _engine.scene().beautyPass()["targetNoise"]=10;
        if (effres.x > 480) {
            _width = (int)effres.x;
            _height= (int)effres.y;
        } else {
            _width = (int)effres.x;
            _height= (int)effres.y;
        }
        _camXform = _engine.scene().cameraPath().parent();
        _camLens = _engine.scene().camera()["lens"].asNode();
        _skBitmap = new SKBitmap( (int)effres.x, (int)effres.y);
        _engine.start();
    }
    public void onError( string pass, string msg) {
        Console.WriteLine("ERROR: "+msg);
    }
    public void onProgress( string pass, bella.Progress progress) {
        _progress = (float)progress.progress();
    }

    public void BellaClose() {
        _engine.Dispose();
        _engineObserver.Dispose();
    }
}
