﻿/*
 * Created by SharpDevelop.
 * User: frederik
 * Date: 15/01/2013
 * Time: 9:13
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
#region usings
using System;
using System.Collections.Generic;

using System.ComponentModel.Composition;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;

using LocalMessageBroadcast;

#endregion usings


namespace InterProcessSendReceiveNodes
{
	#region PluginInfo
	[PluginInfo(Name = "S", Category = "Network.Interprocess.Color", 
	            AutoEvaluate = true, 
	            Author="ft", Help = "S/R nodes that allow to communicate between different vvvv instances", Tags = "")]
	#endregion PluginInfo
	public class SColorNode : IPluginEvaluate, IDisposable
	{
		#region pins
		
		[Input("Input Value", DefaultColor = new double[] { 1.0, 1.0, 1.0, 1.0 })]
		IDiffSpread<RGBAColor> FValueIn;

		[Input("SendString", IsSingle = true, DefaultString = "vvvv")]
		IDiffSpread<string> FChannelIn;
		
		#endregion pins

		#region fields
		[Import()]
		ILogger FLogger;
				
		private string logMe = "";
		
		private LocalMessageBroadcastPartner localMessageBroadcastPartner = null;
		
		private bool newPartner = false;
	
		private uint messageVersion = 1; //never 0, so 0 can be used to know someone has never received a msg yet
		
		#endregion fields

		#region helper functions
		public void LogNow(LogType logType, string message) {
			FLogger.Log( logType, message);
		}
 
		public void Log( LogType logType, string message)
		{
			logMe += "\n" + (logType == LogType.Error ? "ERR " : (logType == LogType.Warning ? "WARN " : "")) + message;
		}

		#endregion helper functions
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax) {
			if (FChannelIn.IsChanged) {
				if ( localMessageBroadcastPartner != null ) {
					localMessageBroadcastPartner.Dispose();
					localMessageBroadcastPartner = null;
				}
				LogNow(LogType.Debug, "[S.Color] Try to create new LocalMessageBroadcastPartner on channel " + Utils.GetChannelPrefix(FValueIn) + FChannelIn[0]);
				localMessageBroadcastPartner = new LocalMessageBroadcastPartner("vvvv", Utils.GetChannelPrefix(FValueIn) + FChannelIn[0]);
				if (localMessageBroadcastPartner != null) {
					LogNow(LogType.Debug, "[S.Color] New LocalMessageBroadcastPartner created with id = " + localMessageBroadcastPartner.PartnerId);
					localMessageBroadcastPartner.OnPartnerJoined += 
						delegate(uint partnerId, string partnerName) { 
							newPartner = true;
						};
				}
				else {
					LogNow(LogType.Debug, "[S.Color] Creating new LocalMessageBroadcastPartner FAILED ");
				}
			}
			
			if ( (FValueIn.IsChanged || newPartner) && localMessageBroadcastPartner != null) {
				newPartner = false;
				
				if (FValueIn.IsChanged) {
					//never return 0, because 0 means uninitialized
					if (messageVersion == uint.MaxValue) {
						messageVersion = 0;
					}
					messageVersion++;
				}
				
				//create a message that holds all slices
				byte[] msg = Utils.GenerateMessage(FValueIn, messageVersion);
				
				//LogNow(LogType.Debug, "[S.Color] Trying to send message of size " + msg.Length);
				localMessageBroadcastPartner.BroadcastMessage(msg);
			}
			
			if (logMe.Length > 0) {
				LogNow(LogType.Message, logMe);
				logMe = "";
			}
		}

		public void Dispose() {
			
			// Take yourself off the Finalization queue
			// to prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}

	}
}
