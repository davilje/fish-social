using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Unity fallback consumes the same source-frame definitions as native Overlay.</summary>
    public sealed class DesktopAudioFrameMarkers
    {
        [Serializable] sealed class Document { public int version; public Marker[] markers; }
        [Serializable] sealed class Marker { public string clip, marker, contactLayer; public int sourceIndex, sourceMs; }
        static Marker[] definitions;
        string clip, action, session;
        long ownerEpoch;
        int lastFrame = -1, occurrence;
        readonly HashSet<string> sent = new HashSet<string>();
        readonly Queue<string> sentOrder = new Queue<string>();
        static void Load()
        {
            if(definitions!=null)return;
            definitions=Array.Empty<Marker>();
            var path=Path.Combine(Application.streamingAssetsPath,"Audio","presentation-markers.json");
            try{if(File.Exists(path)){var doc=JsonUtility.FromJson<Document>(File.ReadAllText(path));if(doc?.version==2&&doc.markers!=null)definitions=doc.markers;}}
            catch(Exception e){Debug.LogWarning("[DesktopAudio] Fallback marker definitions unavailable: "+e.Message);}
        }
        public void Published(string sourceClip,int sourceFrame,bool visible,bool hasVerifiedWaterContact=false)
        {
            var audio=DesktopAudioService.Instance;
            if(audio==null||!visible||audio.Presentation.owner!="unity")return;
            var context=audio.Presentation;Load();
            if(session!=context.presentationSessionId||ownerEpoch!=context.ownerEpoch||action!=context.actionId)
            {sent.Clear();sentOrder.Clear();lastFrame=-1;occurrence=0;session=context.presentationSessionId;ownerEpoch=context.ownerEpoch;action=context.actionId;}
            if(clip!=sourceClip){clip=sourceClip;lastFrame=-1;}
            else if(sourceFrame<lastFrame)occurrence++;
            lastFrame=sourceFrame;
            foreach(var marker in definitions)
            {
                if(marker.clip!=sourceClip)continue;
                var onset=marker.marker=="bite.onset"||marker.marker=="result.presented";
                if(!onset&&marker.sourceIndex!=sourceFrame)continue;
                // The home illustration has no authoritative scene water plane. It never invents water contact.
                if(!string.IsNullOrEmpty(marker.contactLayer)&&!hasVerifiedWaterContact)continue;
                var key=(onset?context.eventId:context.actionId)+":"+marker.marker+":"+occurrence;
                if(!sent.Add(key))continue;
                sentOrder.Enqueue(key);while(sentOrder.Count>512)sent.Remove(sentOrder.Dequeue());
                audio.UnityMarker(marker.marker,occurrence);
            }
        }
    }
}
