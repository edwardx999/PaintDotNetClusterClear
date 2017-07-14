// Compiler options:  /unsafe /optimize /debug- /target:library /out:"C:\Users\edwar\Desktop\ClusterClear.dll"
using System;
using System.Runtime;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.AppModel;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;

using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using ColorWheelControl = PaintDotNet.ColorBgra;
using AngleControl = System.Double;
using PanSliderControl = PaintDotNet.Pair<double,double>;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using ReseedButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using RollControl = System.Tuple<double,double,double>;

[assembly: AssemblyTitle("ClusterClear Plugin for Paint.NET")]
[assembly: AssemblyDescription("Cluster Clear selected pixels")]
[assembly: AssemblyConfiguration("cluster clear")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("ClusterClear")]
[assembly: AssemblyCopyright("Copyright © ")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.0.0")]

namespace ClusterClearEffect {
	public class PluginSupportInfo:IPluginSupportInfo {
		public string Author {
			get {
				return ((AssemblyCopyrightAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute),false)[0]).Copyright;
			}
		}
		public string Copyright {
			get {
				return ((AssemblyDescriptionAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyDescriptionAttribute),false)[0]).Description;
			}
		}

		public string DisplayName {
			get {
				return ((AssemblyProductAttribute)base.GetType().Assembly.GetCustomAttributes(typeof(AssemblyProductAttribute),false)[0]).Product;
			}
		}

		public Version Version {
			get {
				return base.GetType().Assembly.GetName().Version;
			}
		}

		public Uri WebsiteUri {
			get {
				return new Uri("http://www.getpaint.net/redirect/plugins.html");
			}
		}
	}

	[PluginSupportInfo(typeof(PluginSupportInfo),DisplayName = "Cluster Clear")]
	public class ClusterClearEffectPlugin:PropertyBasedEffect {
		public static string StaticName {
			get {
				return "Cluster Clear";
			}
		}

		public static Image StaticIcon {
			get {
				return null;
			}
		}

		public static string SubmenuName {
			get {
				return "Object";
			}
		}

		public ClusterClearEffectPlugin()
			: base(StaticName,StaticIcon,SubmenuName,EffectFlags.Configurable) {
		}

		public enum PropertyNames {
			LowerThreshold,
			UpperThreshold,
			Tolerance
		}


		protected override PropertyCollection OnCreatePropertyCollection() {
			List<Property> props = new List<Property>();
			props.Add(new Int32Property(PropertyNames.LowerThreshold,0,0,300));
			props.Add(new Int32Property(PropertyNames.UpperThreshold,50,0,5000));
			props.Add(new Int32Property(PropertyNames.Tolerance,((int)toleranceMax)>>1,0,(int)toleranceMax));

			return new PropertyCollection(props);
		}

		protected override ControlInfo OnCreateConfigUI(PropertyCollection props) {
			ControlInfo configUI = CreateDefaultConfigUI(props);
			configUI.SetPropertyControlValue(PropertyNames.LowerThreshold,ControlInfoPropertyNames.DisplayName,"Cluster Size Lower Threshold");
			configUI.SetPropertyControlValue(PropertyNames.UpperThreshold,ControlInfoPropertyNames.DisplayName,"Cluster Size Upper Threshold");
			configUI.SetPropertyControlValue(PropertyNames.Tolerance,ControlInfoPropertyNames.DisplayName,"Tolerance ‰");

			return configUI;
		}

		static readonly float toleranceMax = 1000;
		protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken,RenderArgs dstArgs,RenderArgs srcArgs) {
			LowerThreshold=newToken.GetProperty<Int32Property>(PropertyNames.LowerThreshold).Value;
			UpperThreshold=newToken.GetProperty<Int32Property>(PropertyNames.UpperThreshold).Value;
			Tolerance=newToken.GetProperty<Int32Property>(PropertyNames.Tolerance).Value;
			Tolerance*=Tolerance/toleranceMax/toleranceMax;

			base.OnSetRenderInfo(newToken,dstArgs,srcArgs);
		}

		protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props) {
			// Change the effect's window title
			props[ControlInfoPropertyNames.WindowTitle].Value="Cluster Clear";
			// Add help button to effect UI
			props[ControlInfoPropertyNames.WindowHelpContentType].Value=WindowHelpContentType.PlainText;
			props[ControlInfoPropertyNames.WindowHelpContent].Value=" v1.0\nCopyright ©2017 by \nAll rights reserved.";
			base.OnCustomizeConfigUIWindowProperties(props);
		}

		bool cleaned=false;
		protected override unsafe void OnRender(Rectangle[] rois,int startIndex,int length) {
			if(length==0) return;


			RectangleRef[] rects = RectanglesToRectangleRefs(rois);
			Array.Sort(rects);
			List<Cluster> clusters=FindClusters(SrcArgs.Surface,rects);

			/*
			List<Cluster> testClusterList=new List<Cluster>();
			Cluster testCluster = new Cluster();
			testCluster.Create(new Point(246,197),SrcArgs.Surface,rects,EnvironmentParameters.PrimaryColor,Tolerance,this);
			testClusterList.Add(testCluster);
			*/
			//if(clusters.Count>3)
			//RenderCluster(DstArgs.Surface,clusters[3]);
			//new game plan, scan limits for ranges, combine ranges into clusters
			RenderClusters(DstArgs.Surface,clusters);
			if(IsCancelRequested)
				CleanUp();

		}

		void CleanUp() {
			if(!cleaned) {
				Surface dst=DstArgs.Surface,src=SrcArgs.Surface;
				int ymax=src.Height;
				int xmax=src.Width;
				for(int y = 0;y<ymax;++y) {
					for(int x = 0;x<xmax;++x) {
						dst[x,y]=src[x,y];
					}
				}
				cleaned=true;
			}
		}

		void RenderClusters(Surface dst,List<Cluster> clusters) {
			foreach(Cluster c in clusters) {
				RenderCluster(dst,c);
			}
			cleaned=false;
		}

		RectangleRef[] RectanglesToRectangleRefs(Rectangle[] orig) {
			RectangleRef[] ret = new RectangleRef[orig.Length];
			for(int i = orig.Length-1;i>=0;--i)
				ret[i]=new RectangleRef(orig[i]);
			return ret;
		}

		#region UICode
		IntSliderControl LowerThreshold = 0;
		IntSliderControl UpperThreshold = 50; //[0,100]Slider 1 Description
		float Tolerance = 50;
		#endregion

		static void CompressRanges(List<RectangleRef> ranges) {
			ranges.Sort();
			for(int i = 1;i<ranges.Count();++i) {
				if(ranges[i-1].r.Left==ranges[i].r.Left&&ranges[i-1].r.Right==ranges[i].r.Right&&ranges[i-1].r.Bottom==ranges[i].r.Top) {
					ranges[i-1]=new RectangleRef(ranges[i-1].r.Location,new Size(ranges[i].r.Width,ranges[i-1].r.Height+ranges[i].r.Height));
					ranges.RemoveAt(i--);
				} /*
				else if(ranges[i-1].r==ranges[i].r) {
					ranges.RemoveAt(--i);
				}*/
			}
		}

		List<RectangleRef> FindRanges(Surface src,RectangleRef[] limits) {
			ColorBgra PrimaryColor=EnvironmentParameters.PrimaryColor;
			List<RectangleRef> ranges=new List<RectangleRef>();
			byte rangeFound=0;
			int rangeStart=0,rangeEnd=0;
			foreach(RectangleRef rr in limits) {
				for(int y = rr.r.Top;y<rr.r.Bottom;++y) {
					if(IsCancelRequested) goto endloop;
					for(int x = rr.r.Left;x<rr.r.Right;++x) {
						switch(rangeFound) {
							case 0: {
									if(ColorPercentage(src[x,y],PrimaryColor)<=Tolerance) {
										rangeFound=1;
										rangeStart=x;
									}
									break;
								}
							case 1: {
									if(ColorPercentage(src[x,y],PrimaryColor)>Tolerance) {
										rangeFound=2;
										rangeEnd=x;
										goto case 2;
									}
									break;
								}
							case 2: {
									ranges.Add(new RectangleRef(rangeStart,y,rangeEnd-rangeStart,1));
									rangeFound=0;
									break;
								}
						}
					}
					if(1==rangeFound) {
						ranges.Add(new RectangleRef(rangeStart,y,rr.r.Right-rangeStart,1));
						rangeFound=0;
					}
				}
			}
			endloop:
			CompressRanges(ranges);
			return ranges;
		}

		List<Cluster> FormClusters(List<RectangleRef> limits) {
			ColorBgra PrimaryColor=EnvironmentParameters.PrimaryColor;
			//ColorBgra SecondaryColor=(ColorBgra)EnvironmentParameters.SecondaryColor;
			List<Cluster> clusters = new List<Cluster>();
			//new game plan, scan limits for ranges, combine ranges into clusters
			//dumb n² algorithm
			
			return clusters;
		}

		class ClusterPart {
			Cluster parent;
			RectangleRef child;
		}

		class ScanRange {
			public int left,right,y,direction;
			public ScanRange(int left,int right,int y,int direction) {
				this.left=left;
				this.right=right;
				this.y=y;
				this.direction=direction;
			}
		}

		RectangleRef FindRectangleSorted(Point target,RectangleRef[] limits) {
			foreach(RectangleRef r in limits) {
				if(r.r.Left>target.X)
					return null;
				if(r.Contains(target))
					return r;
			}
			return null;
		}

		class RectangleRef:IComparable {
			public Rectangle r;
			public RectangleRef(int x,int y,int width,int height) {
				r=new Rectangle(x,y,width,height);
			}
			public RectangleRef(Point loc,Size size) {
				r=new Rectangle(loc,size);
			}
			public RectangleRef(Rectangle r) {
				this.r=r;
			}
			public Boolean Contains(int x,int y) {
				return Contains(new Point(x,y));
			}
			public Boolean Contains(Point p) {
				return r.Contains(p);
			}
			public int Area() {
				return r.Width*r.Height;
			}
			public int CompareTo(object obj) {
				RectangleRef other = (RectangleRef)obj;
				int xdif = this.r.X-other.r.X;
				if(0!=xdif) return xdif;
				return this.r.Y-other.r.Y;
			}
		}

		class Cluster {
			public List<RectangleRef> ranges;
			//private bool sorted=false;

			public Cluster() {
				ranges=new List<RectangleRef>();
			}

			public int NumPixels() {
				int sum = 0;
				foreach(RectangleRef r in ranges) sum+=r.Area();
				return sum;
			}

			public RectangleRef Contains(Point p) {
				//if(!sorted) { ranges.Sort(); sorted=true; }
				foreach(RectangleRef r in ranges) {
					if(r.Contains(p)) { return r; }
					if(r.r.Left>p.X) { return null; }
				}
				return null;
			}

			public void Create(Point seed,Surface src,RectangleRef[] limits,ColorBgra color,float Tolerance,ClusterClearEffectPlugin controller) {
				ranges.Clear();
				int xL = seed.X;
				while(xL>=0&&ColorPercentage(color,src[seed.X,seed.Y])<=Tolerance) {
					--xL;
				}
				++xL;
				int xR = seed.X+1;
				int maxR = src.Width;
				while(xR<maxR&&ColorPercentage(color,src[xR,seed.Y])<=Tolerance) {
					++xR;
				}
				--xR;
				ranges.Add(new RectangleRef(xL,seed.Y,xR-xL+1,1));

				Stack<ScanRange> scanRanges = new Stack<ScanRange>();
				scanRanges.Push(new ScanRange(xL,xR,seed.Y,1));
				scanRanges.Push(new ScanRange(xL,xR,seed.Y,-1));
				int xMin = 0;
				int xMax = src.Width-1;
				int yMin = 0;
				int yMax = src.Height-1;
				ScanRange r;
				int sleft;
				int sright;
				while(scanRanges.Count!=0) {
					if(controller.IsCancelRequested) return;
					r=scanRanges.Pop();
					//scan left
					for(sleft=r.left-1;sleft>=xMin&&ColorPercentage(color,src[sleft,r.y])<=Tolerance;--sleft) {
					}
					++sleft;

					//scan right
					for(sright=r.right+1;sright<=xMax&&ColorPercentage(color,src[sright,r.y])<=Tolerance;++sright) {
					}
					--sright;
					ranges.Add(new RectangleRef(sleft,r.y,sright-sleft,1));

					//scan in same direction vertically
					bool rangeFound = false;
					int rangeStart = 0;
					int newy = r.y+r.direction;
					if(newy>=yMin&&newy<=yMax) {
						xL=sleft;
						while(xL<=sright) {
							for(;xL<=sright;++xL) {
								if(ColorPercentage(color,src[xL,newy])<=Tolerance) {
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<=sright;++xL) {
								if(ColorPercentage(color,src[xL,newy])>Tolerance)
									break;
							}
							if(rangeFound) {
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,r.direction));
							}
						}
					}

					//scan opposite direction vertically
					newy=r.y-r.direction;
					if(newy>=yMin&&newy<=yMax) {
						xL=sleft;
						while(xL<r.left) {
							for(;xL<r.left;++xL) {
								if(ColorPercentage(color,src[xL,newy])<=Tolerance) {
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<r.left;++xL) {
								if(ColorPercentage(color,src[xL,newy])>Tolerance)
									break;
							}
							if(rangeFound) {
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,-r.direction));
							}
						}
						xL=r.right+1;
						while(xL<=sright) {
							for(;xL<=sright;++xL) {
								if(ColorPercentage(color,src[xL,newy])<=Tolerance) {
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<=sright;++xL) {
								if(ColorPercentage(color,src[xL,newy])>Tolerance)
									break;
							}
							if(rangeFound) {
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,-r.direction));
							}
						}
					}
				}
				CompressRanges();
			}

			public void CompressRanges() {
				ClusterClearEffectPlugin.CompressRanges(ranges);
			}
		}

		void RenderCluster(Surface dst,Cluster cluster) {
			ColorBgra SecondaryColor = (ColorBgra)EnvironmentParameters.SecondaryColor;
			int clusterSize = cluster.NumPixels();
			//bool properSize = (clusterSize>=LowerThreshold&&clusterSize<=UpperThreshold);
			if(clusterSize>=LowerThreshold&&clusterSize<=UpperThreshold) {
				foreach(RectangleRef r in cluster.ranges) {
					for(int y = r.r.Top;y<r.r.Bottom;++y) {
						if(IsCancelRequested) return;
						for(int x = r.r.Left;x<r.r.Right;++x) {
							dst[x,y]=SecondaryColor;
						}
					}
				}
			}
		}

		const float maxDif = 255.0f*255.0f*3.0f;
		static float ColorPercentage(ColorBgra a,ColorBgra b) {
			var dR = (float)(a.R-b.R);
			var dG = (float)(a.G-b.G);
			var dB = (float)(a.B-b.G);
			return (dR*dR+dG*dG+dB*dB)/maxDif;
		}
	}
}
