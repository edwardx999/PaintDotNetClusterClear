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
				return "Cluster Clear Alt";
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
			SelLower, SelUpper,
			SizeLower, SizeUpper,
			ReqLower, ReqUpper
		}

		protected override PropertyCollection OnCreatePropertyCollection() {
			List<Property> props = new List<Property>();
			props.Add(new Int32Property(PropertyNames.SelLower,0,0,255));
			props.Add(new Int32Property(PropertyNames.SelUpper,254,0,255));
			props.Add(new Int32Property(PropertyNames.ReqLower,0,0,255));
			props.Add(new Int32Property(PropertyNames.ReqUpper,100,0,255));
			props.Add(new Int32Property(PropertyNames.SizeLower,0,0,300));
			props.Add(new Int32Property(PropertyNames.SizeUpper,45,0,1000));

			List<PropertyCollectionRule> propRules=new List<PropertyCollectionRule>(){
				new SoftMutuallyBoundMinMaxRule<int,Int32Property>(PropertyNames.SelLower,PropertyNames.SelUpper),
				new SoftMutuallyBoundMinMaxRule<int,Int32Property>(PropertyNames.ReqLower,PropertyNames.ReqUpper),
				new SoftMutuallyBoundMinMaxRule<int,Int32Property>(PropertyNames.SizeLower,PropertyNames.SizeUpper)
			};
			return new PropertyCollection(props,propRules);
		}

		protected override ControlInfo OnCreateConfigUI(PropertyCollection props) {
			ControlInfo configUI = CreateDefaultConfigUI(props);

			configUI.SetPropertyControlValue(
				PropertyNames.SizeLower,
				ControlInfoPropertyNames.DisplayName,
				"Cluster Size Lower Threshold");
			configUI.SetPropertyControlValue(
				PropertyNames.SizeUpper,
				ControlInfoPropertyNames.DisplayName,
				"Cluster Size Upper Threshold");

			configUI.SetPropertyControlValue(
				PropertyNames.ReqLower,
				ControlInfoPropertyNames.DisplayName,
				"Required Color Lower Threshold");
			configUI.SetPropertyControlValue(
				PropertyNames.ReqUpper,
				ControlInfoPropertyNames.DisplayName,
				"Required Color Upper Threshold");

			configUI.SetPropertyControlValue(
				PropertyNames.SelLower,
				ControlInfoPropertyNames.DisplayName,
				"Selection Range Lower Threshold");
			configUI.SetPropertyControlValue(
				PropertyNames.SelUpper,
				ControlInfoPropertyNames.DisplayName,
				"Selection Range Upper Threshold");
			return configUI;
		}

		bool ClustersFinished=false;
		List<Cluster> clusters;
		protected override void OnSetRenderInfo(PropertyBasedEffectConfigToken newToken,RenderArgs dstArgs,RenderArgs srcArgs) {

			SelLower=newToken.GetProperty<Int32Property>(PropertyNames.SelLower).Value;
			SelUpper=newToken.GetProperty<Int32Property>(PropertyNames.SelUpper).Value;
			SizeLower=newToken.GetProperty<Int32Property>(PropertyNames.SizeLower).Value;
			SizeUpper=newToken.GetProperty<Int32Property>(PropertyNames.SizeUpper).Value;
			ReqLower=newToken.GetProperty<Int32Property>(PropertyNames.ReqLower).Value;
			ReqUpper=newToken.GetProperty<Int32Property>(PropertyNames.ReqUpper).Value;

			base.OnSetRenderInfo(newToken,dstArgs,srcArgs);

			PdnRegion selection=EnvironmentParameters.GetSelectionAsPdnRegion();
			CustomOnRender(selection.GetRegionScansInt());
		}

		protected override void OnCustomizeConfigUIWindowProperties(PropertyCollection props) {
			props[ControlInfoPropertyNames.WindowTitle].Value="Cluster Clear";
			props[ControlInfoPropertyNames.WindowHelpContentType].Value=WindowHelpContentType.PlainText;
			props[ControlInfoPropertyNames.WindowHelpContent].Value=" v1.0\nCopyright ©2017 by \nAll rights reserved.";
			base.OnCustomizeConfigUIWindowProperties(props);
		}

		//I should change this to clean up old clusters
		void CustomOnRender(IEnumerable<Rectangle> rois) {
			if(!ClustersFinished)
			{
				SynchronizedCollection<List<Rectangle>> allRanges=new SynchronizedCollection<List<Rectangle>>();
				Parallel.ForEach(rois,rect => {
					CleanUp(rect);
					List<Rectangle> ranges=FindRanges(rect,(byte)SelLower,(byte)SelUpper);
					allRanges.Add(ranges);
				});
				clusters=ClusterRanges(allRanges);
			}
			else
			{
				Parallel.ForEach(rois,rect => {
					CleanUp(rect);
				});
			}
			Parallel.ForEach(clusters,clust => {
				RenderCluster(DstArgs.Surface,SrcArgs.Surface,clust);
			});
		}

		protected override unsafe void OnRender(Rectangle[] rois,int startIndex,int length) {
			return;
		}

		void CleanUp(Rectangle r) {
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

		IntSliderControl SelLower = 0;
		IntSliderControl SelUpper = 254;
		IntSliderControl ReqLower = 0;
		IntSliderControl ReqUpper = 100;
		IntSliderControl SizeLower= 0;
		IntSliderControl SizeUpper= 40;

		static byte Brightness(ColorBgra c) {
			return (byte)(Math.Round(((float)c.R+c.G+c.B)/3.0f));
		}
		List<Rectangle> FindRanges(Rectangle rect,byte lower,byte upper) {
			Surface src=SrcArgs.Surface;
			List<Rectangle> ranges=new List<Rectangle>();
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
								var b=Brightness(src[x,y]);
								if(b>=lower&&b<=upper)
								{
									rangeFound=1;
									rangeStart=x;
								}
								break;
							}
						case 1:
							{
								var b=Brightness(src[x,y]);
								if(b<lower||b>upper)
								{
									rangeFound=2;
									rangeEnd=x;
									goto case 2;
								}
								break;
							}
						case 2:
							{
								ranges.Add(new Rectangle(rangeStart,y,rangeEnd-rangeStart,1));
								rangeFound=0;
								break;
							}
					}
				}
				if(1==rangeFound)
				{
					ranges.Add(new Rectangle(rangeStart,y,rect.Right-rangeStart,1));
					rangeFound=0;
				}
			}
			endloop:
			CompressRanges(ranges);
			return ranges;
		}

		static void CompressRanges(List<Rectangle> ranges) {
			ranges.Sort((a,b) => {
				if(a.Left<b.Left)
				{
					return -1;
				}
				if(a.Left>b.Left)
				{
					return 1;
				}
				return a.Top-b.Top;
			});
			for(int i = 1;i<ranges.Count();++i)
			{
				if(ranges[i-1].Left==ranges[i].Left&&ranges[i-1].Right==ranges[i].Right&&ranges[i-1].Bottom==ranges[i].Top)
				{
					ranges[i-1]=new Rectangle(ranges[i-1].Location,new Size(ranges[i].Width,ranges[i-1].Height+ranges[i].Height));
					ranges.RemoveAt(i--);
				} /*
				else if(ranges[i-1].r==ranges[i].r) {
					ranges.RemoveAt(--i);
				}*/
			}
		}

		static bool OverlapsX(Rectangle a,Rectangle b) {
			return (a.Left<b.Right)&&(a.Right>b.Left);
		}

		List<Cluster> ClusterRanges(IEnumerable<List<Rectangle>> ranges) {
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
								if(tests[s].Y==SearchNode.Parent.Rectangle.Top&&OverlapsX(tests[s].Parent.Rectangle,SearchNode.Parent.Rectangle))
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
								if(tests[s].Y==SearchNode.Parent.Rectangle.Bottom&&OverlapsX(tests[s].Parent.Rectangle,SearchNode.Parent.Rectangle))
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
			public readonly Rectangle Rectangle;
			public ClusterPart(Rectangle rr) {
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
			public List<Rectangle> Ranges;

			public Cluster() {
				Ranges=new List<Rectangle>();
			}

			public int NumPixels {
				get {
					int sum = 0;
					foreach(Rectangle r in Ranges) sum+=(r.Height*r.Width);
					return sum;
				}
			}

			public Rectangle Contains(Point p) {
				//if(!sorted) { ranges.Sort(); sorted=true; }
				foreach(Rectangle r in Ranges)
				{
					if(r.Contains(p)) { return r; }
				}
				return new Rectangle(0,0,0,0);
			}

			public void CompressRanges() {
				ClusterClearEffectPlugin.CompressRanges(Ranges);
			}
		}

		void RenderCluster(Surface dst,Surface src,Cluster cluster) {
			ColorBgra SecondaryColor = EnvironmentParameters.SecondaryColor;
			int clusterSize = cluster.NumPixels;
			if(clusterSize>=SizeLower&&clusterSize<=SizeUpper)
			{
				FillCluster(dst,cluster,SecondaryColor);
			}
			else if(SelLower>=ReqLower&&SelUpper<=ReqUpper)
			{
				return;
			}
			foreach(Rectangle r in cluster.Ranges)
			{
				for(int y = r.Top;y<r.Bottom;++y)
				{
					for(int x = r.Left;x<r.Right;++x)
					{
						var b=Brightness(src[x,y]);
						if(b>=ReqLower&&b<=ReqUpper)
						{
							return;
						}
					}
				}
			}
			FillCluster(dst,cluster,SecondaryColor);
		}

		void FillCluster(Surface dst,Cluster cluster,ColorBgra color) {
			foreach(Rectangle r in cluster.Ranges)
			{
				for(int y = r.Top;y<r.Bottom;++y)
				{
					//if(IsCancelRequested) return;
					for(int x = r.Left;x<r.Right;++x)
					{
						dst[x,y]=color;
					}
				}
			}
		}
	}
}
