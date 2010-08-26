﻿using System;
using System.Collections;
using System.Collections.Generic;

using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;

namespace VVVV.Hosting.Pins
{
	/// <summary>
	/// The spread data type
	/// </summary>
	public class Spread<T> : ISpread<T>
	{
		protected T[] FData;
		protected int FSliceCount;
		
		public Spread(int size)
		{
			FData = new T[0];
			SliceCount = size;
		}
		
		public T this[int index] 
		{
			get 
			{
				return FData[index % FSliceCount];
			}
			set 
			{
				FData[index % FSliceCount] = value;
			}
		}
		
		public int SliceCount 
		{
			get
			{
				return FSliceCount;
			}
			set
			{
				if (FSliceCount != value)
				{
					var old = FData;
					FData = new T[value];
					
					if (old.Length > 0)
					{
						for (int i = 0; i < FData.Length; i++)
						{
							FData[i] = old[i % old.Length];
						}
					}
					else
					{
						for (int i = 0; i < FData.Length; i++)
						{
							FData[i] = default(T);
						}
					}
				}
				
				FSliceCount = value;
			}
		}
		
		public IEnumerator<T> GetEnumerator()
		{
			for (int i=0; i<FSliceCount; i++)
				yield return FData[i];
		}
		
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}