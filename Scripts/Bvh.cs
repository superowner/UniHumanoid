﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace UniHumanoid
{
    public class BvhException : Exception
    {
        public BvhException(string msg) : base(msg) { }
    }

    public enum Channel
    {
        Xposition,
        Yposition,
        Zposition,
        Xrotation,
        Yrotation,
        Zrotation,
    }

    public class BvhNode
    {
        public String Name
        {
            get;
            private set;
        }

        public Channel[] Channels
        {
            get;
            private set;
        }

        public List<BvhNode> Children
        {
            get;
            private set;
        }

        public BvhNode(string name)
        {
            Name = name;
            Children = new List<BvhNode>();
        }

        public virtual void Parse(StringReader r)
        {
            // offset
            r.ReadLine();

            Channels = ParseChannel(r.ReadLine());
        }

        static Channel[] ParseChannel(string line)
        {
            // channels
            var splited = line.Trim().Split();
            if (splited[0] != "CHANNELS")
            {
                throw new BvhException("CHANNELS is not found");
            }
            var count = int.Parse(splited[1]);
            if (count + 2 != splited.Length)
            {
                throw new BvhException("channel count is not match with splited count");
            }
            return splited.Skip(2).Select(x => (Channel)Enum.Parse(typeof(Channel), x)).ToArray();
        }

        public IEnumerable<BvhNode> Traverse()
        {
            yield return this;

            foreach (var child in Children)
            {
                foreach (var descentant in child.Traverse())
                {
                    yield return descentant;
                }
            }
        }
    }

    public class EndSite : BvhNode
    {
        public EndSite(): base("")
        {
        }

        public override void Parse(StringReader r)
        {
            r.ReadLine(); // offset
        }
    }

    public class ChannelCurve
    {
        public float[] Keys
        {
            get;
            private set;
        }

        public ChannelCurve(int frameCount)
        {
            Keys = new float[frameCount];
        }

        public void SetKey(int frame, float value)
        {
            Keys[frame] = value;
        }
    }

    public class Bvh
    {
        public BvhNode Root
        {
            get;
            private set;
        }

        public TimeSpan FrameTime
        {
            get;
            private set;
        }

        public ChannelCurve[] Channels
        {
            get;
            private set;
        }

        int m_frames;

        public override string ToString()
        {
            return string.Format("{0}nodes, {1}channels, {2}frames, {3:0.00}seconds"
                , Root.Traverse().Count()
                , Channels.Length
                , m_frames
                , m_frames * FrameTime.TotalSeconds);
        }

        public Bvh(BvhNode root, int frames, float seconds)
        {
            Root = root;
            FrameTime = TimeSpan.FromSeconds(seconds);
            m_frames = frames;
            var channelCount = Root.Traverse()
                .Where(x => x.Channels!=null)
                .Select(x => x.Channels.Length)
                .Sum();
            Channels = Enumerable.Range(0, channelCount)
                .Select(x => new ChannelCurve(frames))
                .ToArray()
                ;
        }

        public void ParseFrame(int frame, string line)
        {
            var splited = line.Trim().Split().Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (splited.Length != Channels.Length)
            {
                throw new BvhException("frame key count is not match channel count");
            }
            for(int i=0; i<Channels.Length; ++i)
            {
                Channels[i].SetKey(frame, float.Parse(splited[i]));
            }
        }

        public static Bvh Parse(string src)
        {
            using (var r = new StringReader(src))
            {
                if (r.ReadLine() != "HIERARCHY")
                {
                    throw new BvhException("not start with HIERARCHY");
                }
               
                var root = ParseNode(r);
                if (root == null)
                {
                    return null;
                }

                if (r.ReadLine() != "MOTION")
                {
                    throw new BvhException("MOTION is not found");
                }

                var frameSplited=r.ReadLine().Split(':');
                if(frameSplited[0]!= "Frames")
                {
                    throw new BvhException("Frames is not found");
                }
                var frames = int.Parse(frameSplited[1]);

                var frameTimeSplited = r.ReadLine().Split(':');
                if(frameTimeSplited[0]!= "Frame Time")
                {
                    throw new BvhException("Frame Time is not found");
                }
                var frameTime = float.Parse(frameTimeSplited[1]);

                var bvh = new Bvh(root, frames, frameTime);

                for(int i=0; i<frames; ++i)
                {
                    var line = r.ReadLine();
                    bvh.ParseFrame(i, line);
                }
               
                return bvh;
            }
        }

        static BvhNode ParseNode(StringReader r, int level = 0)
        {
            var firstline = r.ReadLine().Trim();
            var splited = firstline.Split();
            if (splited.Length != 2)
            {
                if (splited.Length == 1)
                {
                    if(splited[0] == "}")
                    {
                        return null;
                    }
                }
                throw new BvhException(String.Format("splited to {0}({1})", splited.Length, firstline));
            }

            BvhNode node = null;
            if (splited[0] == "ROOT")
            {
                if (level != 0)
                {
                    throw new BvhException("nested ROOT");
                }
                node = new BvhNode(splited[1]);
            }
            else if (splited[0] == "JOINT")
            {
                if (level == 0)
                {
                    throw new BvhException("should ROOT, but JOINT");
                }
                node = new BvhNode(splited[1]);
            }
            else if (splited[0] == "End")
            {
                if (level == 0)
                {
                    throw new BvhException("End in level 0");
                }
                node = new EndSite();
            }
            else
            {
                throw new BvhException("unknown type: " + splited[0]);
            }

            if(r.ReadLine().Trim() != "{")
            {
                throw new BvhException("'{' is not found");
            }

            node.Parse(r);

            // child nodes
            while (true)
            {
                var child = ParseNode(r, level + 1);
                if (child == null)
                {
                    break;
                }

                if(!(child is EndSite))
                {
                    node.Children.Add(child);
                }
            }

            return node;
        }
    }
}