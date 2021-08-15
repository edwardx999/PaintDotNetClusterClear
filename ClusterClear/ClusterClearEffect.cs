/*
Copyright(C) 2017 Edward Xie

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program. If not, see <https://www.gnu.org/licenses/>.
*/
// Compiler options:  /unsafe /optimize /debug- /target:library /out:"C:\Users\edwar\Desktop\ClusterClear.dll"
using System;
using System.Runtime;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
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
using PaintDotNotExtraUtils;

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
				return "Noise";
			}
		}

		public ClusterClearEffectPlugin()
			: base(StaticName,StaticIcon,SubmenuName,new EffectOptions { Flags = EffectFlags.Configurable }) {
		}

		public enum PropertyNames {
			Mode,
			LowerThreshold,
			UpperThreshold,
			Tolerance
		}

		enum Modes {
			IsNotSecondary,
			IsPrimary
		}
		static readonly int MaxMode=Enum.GetValues(typeof(Modes)).Cast<int>().Max();

		protected override PropertyCollection OnCreatePropertyCollection() {
			List<Property> props = new List<Property>();
			props.Add(StaticListChoiceProperty.CreateForEnum<Modes>(PropertyNames.Mode,Modes.IsNotSecondary,false));
			props.Add(new Int32Property(PropertyNames.LowerThreshold,0,0,300));
			props.Add(new Int32Property(PropertyNames.UpperThreshold,150,0,500));
			props.Add(new Int32Property(PropertyNames.Tolerance,((int)(toleranceMax*0.042)),0,(int)toleranceMax));

			List<PropertyCollectionRule> propRules=new List<PropertyCollectionRule>(){
				new SoftMutuallyBoundMinMaxRule<int,Int32Property>(PropertyNames.LowerThreshold,PropertyNames.UpperThreshold)
			};
			return new PropertyCollection(props,propRules);
		}

		protected override ControlInfo OnCreateConfigUI(PropertyCollection props) {
			ControlInfo configUI = CreateDefaultConfigUI(props);
			configUI.SetPropertyControlType(PropertyNames.Mode,PropertyControlType.RadioButton);
			PropertyControlInfo modes=configUI.FindControlForPropertyName(PropertyNames.Mode);
			modes.SetValueDisplayName(Modes.IsNotSecondary,"Ignore Within Tolerance of Secondary Color");
			modes.SetValueDisplayName(Modes.IsPrimary,"Add Within Tolerance of Primary Color");

			configUI.SetPropertyControlValue(PropertyNames.Mode,ControlInfoPropertyNames.DisplayName,"Clustering Mode");
			configUI.SetPropertyControlValue(PropertyNames.LowerThreshold,ControlInfoPropertyNames.DisplayName,"Cluster Size Lower Threshold");
			configUI.SetPropertyControlValue(PropertyNames.UpperThreshold,ControlInfoPropertyNames.DisplayName,"Cluster Size Upper Threshold");
			configUI.SetPropertyControlValue(PropertyNames.Tolerance,ControlInfoPropertyNames.DisplayName,"Tolerance ‰");

			return configUI;
		}

		static readonly float toleranceMax = 1000;
		bool ClustersFinished=false;
		bool ToleranceChanged=true;
		bool ModeChanged=true;
		List<Cluster> clusters;
		protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken,RenderArgs dstArgs,RenderArgs srcArgs) {
			Modes OldMode=Mode;
			Mode=(Modes)newToken.GetProperty<StaticListChoiceProperty>(PropertyNames.Mode).Value;
			ModeChanged=Mode!=OldMode;
			LowerThreshold=newToken.GetProperty<Int32Property>(PropertyNames.LowerThreshold).Value;

			UpperThreshold=newToken.GetProperty<Int32Property>(PropertyNames.UpperThreshold).Value;

			float OldTolerance=Tolerance;
			Tolerance=newToken.GetProperty<Int32Property>(PropertyNames.Tolerance).Value;
			Tolerance*=Tolerance/toleranceMax/toleranceMax;
			ToleranceChanged=Tolerance!=OldTolerance;

			base.OnSetRenderInfo(newToken,dstArgs,srcArgs);

			PdnRegion selection=EnvironmentParameters.GetSelectionAsPdnRegion();
			List<RectangleRef> selRects=RectangleRef.RectanglesToRectangleRefs(selection.GetRegionScansInt());
			CustomOnRender(RectangleRef.SplitSmall(selRects,selection.GetBoundsInt().Bottom/4));
		}

		protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props) {
			props[ControlInfoPropertyNames.WindowTitle].Value="Cluster Clear";
			props[ControlInfoPropertyNames.WindowHelpContentType].Value=WindowHelpContentType.PlainText;
			props[ControlInfoPropertyNames.WindowHelpContent].Value=" v1.0\nCopyright ©2017 by \nAll rights reserved.";
			base.OnCustomizeConfigUIWindowProperties(props);
		}

		//I should change this to clean up old clusters
		void CustomOnRender(IEnumerable<RectangleRef> rois) {
			if(ToleranceChanged||!ClustersFinished||ModeChanged)
			{
				SynchronizedCollection<List<RectangleRef>> allRanges=new SynchronizedCollection<List<RectangleRef>>();
				switch(Mode)
				{
					case Modes.IsPrimary:
						{
							ColorBgra PrimaryColor=EnvironmentParameters.PrimaryColor;
							Parallel.ForEach(rois,rect => {
								CleanUp(rect);
								List<RectangleRef> ranges=FindRanges(rect,PrimaryColor,false);
								allRanges.Add(ranges);
							});
							break;
						}
					case Modes.IsNotSecondary:
						{
							ColorBgra SecondaryColor=EnvironmentParameters.SecondaryColor;
							Parallel.ForEach(rois,rect => {
								CleanUp(rect);
								List<RectangleRef> ranges=FindRanges(rect,SecondaryColor,true);
								allRanges.Add(ranges);
							});
							break;
						}
				}
				clusters=ClusterRanges(allRanges);
			}
			else
			{
				Parallel.ForEach(rois,rect => {
					CleanUp(rect);
				});
			}
			Parallel.ForEach(clusters,clust => {
				RenderCluster(DstArgs.Surface,clust);
			});
		}

		protected override unsafe void OnRender(Rectangle[] rois,int startIndex,int length) {
			return;
		}

		void CleanUp(RectangleRef r) {
			Surface dst=DstArgs.Surface,src=SrcArgs.Surface;
			int ymax=r.Bottom;
			int xmax=r.Right;
			for(int y = r.Top;y<ymax;++y)
			{
				for(int x = r.Left;x<xmax;++x)
				{
					dst[x,y]=src[x,y];
				}
			}
		}

		Modes Mode;
		IntSliderControl LowerThreshold = 0;
		IntSliderControl UpperThreshold = 50;
		float Tolerance = -5;

		List<RectangleRef> FindRanges(RectangleRef rect,ColorBgra Color,bool IgnoreWithinTolerance) {
			Surface src=SrcArgs.Surface;
			List<RectangleRef> ranges=new List<RectangleRef>();
			byte rangeFound=0;
			int rangeStart=0,rangeEnd=0;
			for(int y = rect.Top;y<rect.Bottom;++y)
			{
				if(IsCancelRequested) goto endloop;
				for(int x = rect.Left;x<rect.Right;++x)
				{
					switch(rangeFound)
					{
						case 0:
							{
								if(ColorUtils.RGBPercentage(src[x,y],Color)<=Tolerance^IgnoreWithinTolerance)
								{
									rangeFound=1;
									rangeStart=x;
								}
								break;
							}
						case 1:
							{
								if(ColorUtils.RGBPercentage(src[x,y],Color)>Tolerance^IgnoreWithinTolerance)
								{
									rangeFound=2;
									rangeEnd=x;
									goto case 2;
								}
								break;
							}
						case 2:
							{
								ranges.Add(new RectangleRef(rangeStart,y,rangeEnd-rangeStart,1));
								rangeFound=0;
								break;
							}
					}
				}
				if(1==rangeFound)
				{
					ranges.Add(new RectangleRef(rangeStart,y,rect.Right-rangeStart,1));
					rangeFound=0;
				}
			}
			endloop:
			CompressRanges(ranges);
			return ranges;
		}

		static void CompressRanges(List<RectangleRef> ranges) {
			ranges.Sort();
			for(int i = 1;i<ranges.Count();++i)
			{
				if(ranges[i-1].rect.Left==ranges[i].rect.Left&&ranges[i-1].rect.Right==ranges[i].rect.Right&&ranges[i-1].rect.Bottom==ranges[i].rect.Top)
				{
					ranges[i-1]=new RectangleRef(ranges[i-1].rect.Location,new Size(ranges[i].rect.Width,ranges[i-1].rect.Height+ranges[i].rect.Height));
					ranges.RemoveAt(i--);
				} /*
				else if(ranges[i-1].r==ranges[i].r) {
					ranges.RemoveAt(--i);
				}*/
			}
		}

		List<Cluster> ClusterRanges(IEnumerable<List<RectangleRef>> ranges) {
			ClustersFinished=false;
			List<ClusterTestNode> tests=new List<ClusterTestNode>();
			ranges.ForEach(rrl => {
				rrl.ForEach(rr => {
					ClusterPart toAdd=new ClusterPart(rr);
					tests.Add(new ClusterTestNode(toAdd,true));
					tests.Add(new ClusterTestNode(toAdd,false));
				}
				);
			});
			tests.Sort((a,b) => {//sort by y, if ys are same, bottoms are first
				int ydif=a.Y-b.Y;
				if(0!=ydif) return ydif;
				return a.IsTop.CompareTo(b.IsTop);
			});

			List<Cluster> Clusters=new List<Cluster>();
			Stack<int> searchStack=new Stack<int>();
			int max=tests.Count;
			for(int i = max-1;i>=0;--i)
			{
				ClusterTestNode CurrentNode=tests[i];
				if(!CurrentNode.IsTop) continue;//if it is a bottom or is already in a cluster, nothing is done
				if(CurrentNode.Parent.Cluster==null)
				{
					//Console.WriteLine("\nTesting "+CurrentNode.Parent.Rectangle);
					Cluster CurrentCluster=new Cluster();
					Clusters.Add(CurrentCluster);
					searchStack.Push(i);
					while(searchStack.Count>0)
					{//search for contacts
						if(IsCancelRequested) goto endloop;
						int searchIndex=searchStack.Pop();
						ClusterTestNode SearchNode=tests[searchIndex];
						//Console.WriteLine("\tSearch Seed: "+SearchNode.Parent.Rectangle);
						if(SearchNode.Parent.Cluster!=null) continue;
						SearchNode.Parent.Cluster=CurrentCluster;
						CurrentCluster.Ranges.Add(SearchNode.Parent.Rectangle);
						//search up for bottoms
						for(int s = searchIndex-1;s>=0;--s)
						{
							if(!tests[s].IsTop)
							{
								if(tests[s].Y==SearchNode.Parent.Rectangle.Top&&tests[s].Parent.Rectangle.OverlapsX(SearchNode.Parent.Rectangle))
								{
									searchStack.Push(s);
								}
								else if(tests[s].Y<SearchNode.Parent.Rectangle.Top)
								{
									//Console.WriteLine("\t\tStopped at "+s);
									break;
								}
							}
						}
						//search down for tops
						for(int s = searchIndex+1;s<max;++s)
						{
							if(tests[s].IsTop)
							{
								if(tests[s].Y==SearchNode.Parent.Rectangle.Bottom&&tests[s].Parent.Rectangle.OverlapsX(SearchNode.Parent.Rectangle))
								{
									searchStack.Push(s);
								}
								else if(tests[s].Y>SearchNode.Parent.Rectangle.Bottom)
								{
									//Console.WriteLine("\t\tStopped at "+s);
									break;
								}
							}
						}
					}
				}
			}
			ClustersFinished=true;
			endloop:
			return Clusters;
		}

		class ClusterPart {
			public Cluster Cluster=null;
			public readonly RectangleRef Rectangle;
			public ClusterPart(RectangleRef rr) {
				Rectangle=rr;
			}
		}

		class ClusterTestNode {
			public readonly ClusterPart Parent;
			public readonly bool IsTop;
			public readonly int Y;
			public ClusterTestNode(ClusterPart Parent,bool Top) {
				this.Parent=Parent;
				this.IsTop=Top;
				Y=Top ? Parent.Rectangle.Top : Parent.Rectangle.Bottom;
			}
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

		class Cluster {
			public List<RectangleRef> Ranges;

			public Cluster() {
				Ranges=new List<RectangleRef>();
			}

			public int NumPixels {
				get {
					int sum = 0;
					foreach(RectangleRef r in Ranges) sum+=r.Area();
					return sum;
				}
			}

			public RectangleRef Contains(Point p) {
				//if(!sorted) { ranges.Sort(); sorted=true; }
				foreach(RectangleRef r in Ranges)
				{
					if(r.Contains(p)) { return r; }
					if(r.rect.Left>p.X) { return null; }
				}
				return null;
			}

			public void Create(Point seed,Surface src,RectangleRef[] limits,ColorBgra color,float Tolerance,ClusterClearEffectPlugin controller,bool[,] safePoints) {
				Ranges.Clear();
				int xL = seed.X;
				while(xL>=0&&ColorUtils.RGBPercentage(color,src[xL,seed.Y])<=Tolerance)
				{
					--xL;
				}
				++xL;
				int xR = seed.X+1;
				int maxR = src.Width;
				while(xR<maxR&&ColorUtils.RGBPercentage(color,src[xR,seed.Y])<=Tolerance)
				{
					++xR;
				}
				--xR;
				Ranges.Add(new RectangleRef(xL,seed.Y,xR-xL+1,1));

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
				while(scanRanges.Count!=0)
				{
					if(controller.IsCancelRequested) return;
					r=scanRanges.Pop();
					//scan left
					for(sleft=r.left-1;sleft>=xMin&&ColorUtils.RGBPercentage(color,src[sleft,r.y])<=Tolerance;--sleft)
					{
						safePoints[sleft,r.y]=true;
					}
					++sleft;

					//scan right
					for(sright=r.right+1;sright<=xMax&&ColorUtils.RGBPercentage(color,src[sright,r.y])<=Tolerance;++sright)
					{
						safePoints[sright,r.y]=true;
					}
					--sright;
					Ranges.Add(new RectangleRef(sleft,r.y,sright-sleft,1));

					//scan in same direction vertically
					bool rangeFound = false;
					int rangeStart = 0;
					int newy = r.y+r.direction;
					if(newy>=yMin&&newy<=yMax)
					{
						xL=sleft;
						while(xL<=sright)
						{
							for(;xL<=sright;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])<=Tolerance)
								{
									safePoints[xL,newy]=true;
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<=sright;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])>Tolerance)
								{
									break;
								}
								safePoints[xL,newy]=true;
							}
							if(rangeFound)
							{
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,r.direction));
							}
						}
					}

					//scan opposite direction vertically
					newy=r.y-r.direction;
					if(newy>=yMin&&newy<=yMax)
					{
						xL=sleft;
						while(xL<r.left)
						{
							for(;xL<r.left;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])<=Tolerance)
								{
									safePoints[xL,newy]=true;
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<r.left;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])>Tolerance)
									break;
								safePoints[xL,newy]=true;
							}
							if(rangeFound)
							{
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,-r.direction));
							}
						}
						xL=r.right+1;
						while(xL<=sright)
						{
							for(;xL<=sright;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])<=Tolerance)
								{
									safePoints[xL,newy]=true;
									rangeFound=true;
									rangeStart=xL++;
									break;
								}
							}
							for(;xL<=sright;++xL)
							{
								if(ColorUtils.RGBPercentage(color,src[xL,newy])>Tolerance)
									break;
								safePoints[xL,newy]=true;
							}
							if(rangeFound)
							{
								rangeFound=false;
								scanRanges.Push(new ScanRange(rangeStart,xL-1,newy,-r.direction));
							}
						}
					}
				}
				CompressRanges();
			}

			public void CompressRanges() {
				ClusterClearEffectPlugin.CompressRanges(Ranges);
			}
		}

		void RenderCluster(Surface dst,Cluster cluster) {
			ColorBgra SecondaryColor = EnvironmentParameters.SecondaryColor;
			int clusterSize = cluster.NumPixels;
			if(clusterSize>=LowerThreshold&&clusterSize<=UpperThreshold)
			{
				foreach(RectangleRef r in cluster.Ranges)
				{
					for(int y = r.rect.Top;y<r.rect.Bottom;++y)
					{
						if(IsCancelRequested) return;
						for(int x = r.rect.Left;x<r.rect.Right;++x)
						{
							dst[x,y]=SecondaryColor;
						}
					}
				}
			}
		}
	}
}
