using System.Globalization;

namespace Lumora.Privacy.FingerprintProtection;

// Ajoute un bruit leger et coherent (meme graine pour toute la session, donc
// stable d'une page a l'autre, mais differente a chaque lancement de Lumora)
// aux trois vecteurs de fingerprinting les plus utilises : Canvas, WebGL et
// AudioContext. Normalise aussi hardwareConcurrency/deviceMemory vers des
// valeurs courantes. Aucun serveur, aucun intermediaire reseau : uniquement
// du cote navigateur, comme la position fictive.
internal static class FingerprintProtectionScript
{
    private const string Template = """
        (function(){
        if(window.__lumora_fp_protect)return;
        window.__lumora_fp_protect=true;
        var seed=__SEED__;
        function rnd(){seed=(seed*1103515245+12345)&0x7fffffff;return seed/0x7fffffff;}
        function noisify(imageData){
        var d=imageData.data;
        for(var i=0;i<d.length;i+=4){
        if(rnd()<0.02){d[i]=d[i]^1;d[i+1]=d[i+1]^1;d[i+2]=d[i+2]^1;}
        }
        return imageData;
        }
        var origGetImageData=CanvasRenderingContext2D.prototype.getImageData;
        CanvasRenderingContext2D.prototype.getImageData=function(){
        return noisify(origGetImageData.apply(this,arguments));
        };
        function noisifyCanvas(canvas){
        try{
        var ctx=canvas.getContext('2d');
        if(ctx&&canvas.width>0&&canvas.height>0){
        var imgData=origGetImageData.call(ctx,0,0,canvas.width,canvas.height);
        noisify(imgData);
        ctx.putImageData(imgData,0,0);
        }
        }catch(e){}
        }
        var origToDataURL=HTMLCanvasElement.prototype.toDataURL;
        HTMLCanvasElement.prototype.toDataURL=function(){
        noisifyCanvas(this);
        return origToDataURL.apply(this,arguments);
        };
        var origToBlob=HTMLCanvasElement.prototype.toBlob;
        HTMLCanvasElement.prototype.toBlob=function(){
        noisifyCanvas(this);
        return origToBlob.apply(this,arguments);
        };
        function patchGetParameter(proto){
        if(!proto)return;
        var orig=proto.getParameter;
        proto.getParameter=function(p){
        if(p===37445)return'Generic Vendor';
        if(p===37446)return'Generic Renderer';
        return orig.call(this,p);
        };
        }
        patchGetParameter(window.WebGLRenderingContext&&window.WebGLRenderingContext.prototype);
        patchGetParameter(window.WebGL2RenderingContext&&window.WebGL2RenderingContext.prototype);
        if(window.AudioBuffer){
        var origGetChannelData=AudioBuffer.prototype.getChannelData;
        AudioBuffer.prototype.getChannelData=function(){
        var data=origGetChannelData.apply(this,arguments);
        for(var i=0;i<data.length;i+=100){
        data[i]=data[i]+(rnd()-0.5)*0.0001;
        }
        return data;
        };
        }
        try{Object.defineProperty(navigator,'hardwareConcurrency',{get:function(){return 4;}});}catch(e){}
        try{if('deviceMemory' in navigator)Object.defineProperty(navigator,'deviceMemory',{get:function(){return 8;}});}catch(e){}
        })();
        """;

    public static string Build(long sessionSeed) =>
        Template.Replace("__SEED__", sessionSeed.ToString(CultureInfo.InvariantCulture));
}
