﻿#region usings
using System;
using System.ComponentModel.Composition;
using System.Net;
using System.Text;
using System.Timers;

using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Animation;
using VVVV.Utils.Network;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

#endregion usings

namespace VVVV.Nodes
{
	
	#region PluginInfo
	[PluginInfo(Name = "Sync", 
	Category = "Network", 
	Version = "FileStream",
	Help = "Syncronizes a FileStream node over network in a boygroup setup", 
	Tags = "",
	AutoEvaluate = true)]
	#endregion PluginInfo
	public class FileStreamNetworkSyncNode : IPluginEvaluate, IDisposable
	{
		#region fields & pins
		[Input("Time", IsSingle = true)]
		ISpread<double> FTime;
		
		[Input("Stream Length", IsSingle = true)]
		ISpread<double> FLength;
		
		[Input("Fine Offset", IsSingle = true)]
		ISpread<int> FFineOffset;
		
		[Input("Is Client", IsSingle = true, Visibility = PinVisibility.OnlyInspector)]
		ISpread<bool> FIsClient;
		
		[Input("Server IP", DefaultString = "127.0.0.1", IsSingle = true, StringType = StringType.IP, Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FServerIP;

		[Input("Port", DefaultValue = 3336, IsSingle = true)]
		IDiffSpread<int> FPort;
		
		[Output("Do Seek")]
		ISpread<bool> FDoSeekOut;
		
		[Output("Seek Position")]
		ISpread<double> FSeekTimeOut;
		
		[Output("Adjust System Time")]
		ISpread<double> FAdjustTimeOut;
		
		[Output("Offset")]
		ISpread<double> FOffsetOut;
		
		[Output("Stream Offset")]
		ISpread<double> FStreamOffsetOut;
		
		object FLock = new object();
		double FStreamTime;
		double FTimeStamp;
		double FReceivedStreamTime;
		double FReceivedTimeStamp;
		int FFrameCounter;
		IIRFilter FStreamDiffFilter;
		UDPServer FServer;
		IPEndPoint FRemoteServer;
		Timer FTimer;
		
		[Import]
		IHDEHost FHost;

		[Import]
		ILogger FLogger;
		#endregion fields & pins
		
		public FileStreamNetworkSyncNode()
		{

			FStreamDiffFilter.Value = 0;
			FStreamDiffFilter.Thresh = 1;
			FStreamDiffFilter.Alpha = 0.97;
			
			FTimer = new Timer(500);
			FTimer.Elapsed += FTimer_Elapsed;
			FTimer.Start();
			
		}

		void FTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			//request sync data
			FServer.Send(Encoding.ASCII.GetBytes("videosync"), FRemoteServer);
		}
			
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			//set server and port
			if(FPort.IsChanged)
			{
				if(FServer == null)
				{
					FServer = (FIsClient[0] || FHost.IsBoygroupClient) ? new UDPServer(FPort[0] + 1) : new UDPServer(FPort[0]);
					FServer.MessageReceived += FServer_MessageReceived;
					FServer.Start();
				}
				else
				{
					FServer.Port = (FIsClient[0] || FHost.IsBoygroupClient) ? FPort[0] + 1 : FPort[0];
				}
				
				if(FIsClient[0] || FHost.IsBoygroupClient)
					FRemoteServer = new IPEndPoint(IPAddress.Parse( FIsClient[0] ? FServerIP[0] : FHost.BoygroupServerIP), FPort[0]);
			}
			
			//read stream time
			lock(FLock)
			{
				FStreamTime = FTime[0];
				FTimeStamp = FHost.RealTime;
			}
			
			//do the evaluation for client or server
			if(FIsClient[0] || FHost.IsBoygroupClient)
			{
				ClientEvaluate();
			}
			else
			{
				ServerEvaluate();
			}
		}

		//respond to udp message
		void FServer_MessageReceived(object sender, UDPReceivedEventArgs e)
		{
			if(FIsClient[0] || FHost.IsBoygroupClient)
			{
				ReceiveServerAnswer(e.Data);
			}
			else //server code
			{
				lock(FLock)
				{
					FServer.Send(Encoding.ASCII.GetBytes(FStreamTime.ToString() + ";" + FTimeStamp.ToString()), e.RemoteSender);
					
					FLogger.Log(LogType.Debug, FStreamTime.ToString() + ";" + FHost.RealTime.ToString());
				}
			}
		}
		
		void ReceiveServerAnswer(byte[] data)
		{
			var s = Encoding.ASCII.GetString(data).Split(';');
			
			lock(FLock)
			{
				FReceivedStreamTime = Double.Parse(s[0]);
				FReceivedTimeStamp = Double.Parse(s[1]);
			}
		}
		
		protected void ClientEvaluate()
		{
			var fCount = 10;
			lock(FLock)
			{
				if(FStreamTime > 0.5 && FStreamTime < FLength[0] - 0.5)
				{
					var offset = FTimeStamp - FReceivedTimeStamp;
					var streamDiff = FReceivedStreamTime - FStreamTime + offset + FFineOffset[0] * 0.001;
					var doSeek = Math.Abs(streamDiff) > 1;
					
					FStreamDiffFilter.Update(streamDiff);
					
					FDoSeekOut[0] = doSeek;
					FSeekTimeOut[0] = FReceivedStreamTime + offset + 0.05;
					
					var doAdjust = Math.Abs(FStreamDiffFilter.Value) > 0.005 ? 1 : 0;
					
					if(!doSeek && FFrameCounter == 0)
					{
						FAdjustTimeOut[0] = FStreamDiffFilter.Value * 100 * doAdjust;
					}
					else
					{
						FAdjustTimeOut[0] = 0;
					}
					
					FStreamOffsetOut[0] = streamDiff;
					FOffsetOut[0] = offset;
					
					FFrameCounter = (++FFrameCounter) % fCount;
					
				}
				else //near loop
				{
					FAdjustTimeOut[0] = 0;
					FDoSeekOut[0] = false;
					FFrameCounter = 0;
				}
			}
			
		}
		
		protected void ServerEvaluate()
		{
			
		}
		
		public void Dispose()
		{
			FServer.MessageReceived -= FServer_MessageReceived;
			FTimer.Elapsed -= FTimer_Elapsed;
			FServer.Close();
		}
		
	}
	
}
