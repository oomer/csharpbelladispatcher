using System.Drawing;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using SkiaSharp;
using bella;
using libBella = bella.bella;
using System.Runtime.InteropServices.Marshalling;

namespace oomer_imgui_learn3; 
public class EngineObserver : bella.EngineObserver {
    private IBella _looseCouple;
    public EngineObserver( IBella MyApp ) {
        _looseCouple = MyApp;
        Console.WriteLine( "Engine observed..." ); 
    }
    public override void onImage( string renderpass, bella.Image image ) {
        _looseCouple.ShowImage( renderpass, image ); 
    }

    public override void onSceneLoaded( bella.Scene scene ) {
        _looseCouple.onSceneLoaded( scene ); 
    }
    public override void onError(string pass, string msg) {
        _looseCouple.onError( pass, msg  ); 
    }
    public override void onProgress(string pass, bella.Progress progress) {
        _looseCouple.onProgress( pass, progress  ); 
    }

}
public interface IBella {
    void ShowImage( string pass, bella.Image img );
    void onSceneLoaded( bella.Scene scene );
    void onError( string pass, string msg );
    void onProgress( string pass, bella.Progress progress );
}
static class Program {
    static void Main(string[] args) {
        int _port = 8799;
        string _bellaFile = "";
        if ( args.Length > 0 ) {
            int counter = 0;
            foreach ( string a in args ) {
                if ( a.EndsWith(".bsz") || a.EndsWith(".bsa") || a.EndsWith(".bsx") ) {
                    _bellaFile = a;
                } 
                if ( a == "-p" || a == "--port" ) {
                    _port = a[ counter+1 ];         
                }
            }
            if (_bellaFile == "") {
                Console.WriteLine("A bella .bsz file is required");
                Environment.Exit(1);    
            }
            counter++;
        } else {
            Console.WriteLine("A bella .bsz file is required");
            Environment.Exit(1);    
        }
        var gui =  new GUI( _bellaFile, _port);
        gui.Start();
    }
}
