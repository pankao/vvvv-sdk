﻿/*
 * Created by SharpDevelop.
 * User: frederik
 * Date: 8/01/2013
 * Time: 17:32
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Collections.Concurrent;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Utils.SlimDX;
using SlimDX;
using SlimDX.Direct3D9;


using VVVV.Core.Logging;

using Wyphon;

#endregion usings

namespace VVVV.Nodes.Network.Wyphon
{
	#region PluginInfo
	[PluginInfo(Name = "WyphonReceiveTextures", Category = "Network", 
	            AutoEvaluate = false, 
	            Author="ft", Help = "Share our DX9ex shared textures with other wyhon partner applications", Tags = "")]
	#endregion PluginInfo
	public class WyphonReceiveTexturesNode : IPluginEvaluate, IDisposable
	{
		#region pins
		[Input("Partner Id")]
		IDiffSpread<UInt32> FPartnerIdIn;

		[Input("Do Filter On Partner Id", IsSingle = true)]
		IDiffSpread<bool> FDoFilterIdIn;

		[Input( "Partner Name" )]
		IDiffSpread<string> FPartnerNameIn;

		[Input( "Texture Description" )]
		IDiffSpread<string> FTextureDescriptionIn;

		[Input( "Do Filter On Name/Description", IsSingle = true )]
		IDiffSpread<bool> FDoFilterNameIn;


		[Output( "Partner Id" )]
		ISpread<UInt32> FPartnerIdOut;
		
		[Output("Description")]
		ISpread<string> FDescriptionOut;

		[Output("Width")]
		ISpread<UInt32> FWidthOut;

		[Output("Height")]
		ISpread<UInt32> FHeightOut;
		
		[Output("Format", EnumName = "TextureFormat")]
		ISpread<EnumEntry> FFormatEnumOut;

		[Output("Usage", EnumName = "TextureUsage")]
		ISpread<EnumEntry> FUsageEnumOut;

		
		[Output("Handle")]
		ISpread<UInt32> FHandleOut;

		#endregion pins

		#region fields
		[Import()]
		ILogger FLogger;

		private UInt32 previousTexturesVersion = 0;
						
		private string logMe = "";
		
		private bool disposed = false;
		
		public bool Disposed {
			get { return disposed; }
		}
		
		#endregion fields

		#region helper functions
		
		//only keep the 4 flags that vvvv can handle, and filter out others like Usage.SoftwareProcessing etc.
		private UInt32 usage2enumUsage(UInt32 usage) {
			return usage & ((UInt32)Usage.None | (UInt32)Usage.RenderTarget | (UInt32)Usage.Dynamic | (UInt32)Usage.DepthStencil );
		}


		private bool spreadContains<T>(ISpread<T> spread, T value) {
			foreach (T currVal in spread) {
				if (currVal.Equals(value)) {
					return true;
				}
			}
			return false;
		}

		#endregion helper functions

		

		public WyphonReceiveTexturesNode()
		{
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax) {
			//ignore spreadmax, use FHandle in as the reference spread !!!
			
			if (	WyphonNode.wyphonPartner != null 
			    	&& (
				    	( previousTexturesVersion != WyphonNode.texturesVersion )
						|| FPartnerIdIn.IsChanged
						|| FDoFilterIdIn.IsChanged
						|| FPartnerNameIn.IsChanged
						|| FTextureDescriptionIn.IsChanged
						|| FDoFilterNameIn.IsChanged
					)
				) {
				
				//LogNow(LogType.Debug, "Something happened with the textures shared by others, update our output !");
				
				lock (WyphonNode.sharedTexturesLock) {

					FPartnerIdOut.SliceCount = 0;
					FDescriptionOut.SliceCount = 0;
					FWidthOut.SliceCount = 0;
					FHeightOut.SliceCount = 0;
					FFormatEnumOut.SliceCount = 0;
					FUsageEnumOut.SliceCount = 0;
//					FFormatUintOut.SliceCount = 0;
//					FUsageUintOut.SliceCount = 0;
					FHandleOut.SliceCount = 0;
					
					foreach ( UInt32 partnerId in WyphonNode.SharedTexturesPerPartner.Keys ) {
						if ( ! (FDoFilterIdIn.SliceCount > 0 && FDoFilterIdIn[0]) || FPartnerIdIn.IndexOf(partnerId) >= 0 ) {
							//filter not enabled OR partnerId found in list => add this partner's textures to ouput

							ISpread<SharedTextureInfo> textureInfoSpread;
							if ( WyphonNode.SharedTexturesPerPartner.TryGetValue(partnerId, out textureInfoSpread) && textureInfoSpread != null) {

								foreach ( SharedTextureInfo textureInfo in textureInfoSpread ) {
									//filter on name if necessary
									//Log(LogType.Debug, "Do we utput this texture? Filter on " + FPartnerNameIn + " and "+ FPartnerNameIn.c.Equals( WyphonNode.wyphonPartner.GetPartnerName( partnerId ) ) && FTextureDescriptionIn.Equals( textureInfo.description );
									if ( !( FDoFilterNameIn.SliceCount > 0 && FDoFilterNameIn[0] ) || ( spreadContains( FPartnerNameIn, WyphonNode.wyphonPartner.GetPartnerName( partnerId ) ) && spreadContains( FTextureDescriptionIn, textureInfo.description ) ) ) {

										FPartnerIdOut.Add( partnerId );
										FDescriptionOut.Add( textureInfo.description );
										FWidthOut.Add( textureInfo.width );
										FHeightOut.Add( textureInfo.height );
									
										Format format = (Format) textureInfo.format;
										EnumEntry formatEnumEntry = EnumManager.GetEnumEntry("TextureFormat", 0);
										for ( int i = 0; i < EnumManager.GetEnumEntryCount("TextureFormat"); i++ ) {
											if ( EnumManager.GetEnumEntryString("TextureFormat", i).CompareTo( format.ToString() ) == 0 ) {
												formatEnumEntry = EnumManager.GetEnumEntry("TextureFormat", i);
											}
										}
									
										//GetEnumEntryByUint("TextureFormat", textureInfo.format);
										EnumEntry usageEnumEntry = EnumManager.GetEnumEntry("TextureUsage", (int) usage2enumUsage(textureInfo.usage) );
									
										LogNow ( LogType.Debug, "FORMAT Received = " + formatEnumEntry.ToString() + " index=" + formatEnumEntry.Index + " D3D const = " + textureInfo.format);
									
										FFormatEnumOut.Add(formatEnumEntry);
										FUsageEnumOut.Add(usageEnumEntry);
									
	//									FFormatUintOut.Add(textureInfo.format);
	//									FUsageUintOut.Add(textureInfo.usage);
										FHandleOut.Add(textureInfo.textureHandle);

									}
								}
							}
						}
					}
				}
				
				previousTexturesVersion = WyphonNode.texturesVersion;
			}
			
			if (logMe.Length > 0) {
				LogNow(LogType.Message, logMe);
				logMe = "";
			}			
		}
		
		public void LogNow(LogType logType, string message) {
			FLogger.Log( logType, message);
		}
 
		public void Log( LogType logType, string message)
		{
			logMe += "\n" + (logType == LogType.Error ? "ERR " : (logType == LogType.Warning ? "WARN " : "")) + message;
		}


		public void Dispose() {
			if ( ! disposed ) {
				disposed = true;
				
				// Take yourself off the Finalization queue
				// to prevent finalization code for this object
				// from executing a second time.
				GC.SuppressFinalize(this);
			}
		}
	}

}