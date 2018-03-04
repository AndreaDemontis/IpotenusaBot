using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ipotenusa.Utils
{
	public class ConcurrentFixedSizedQueue<T> : ConcurrentQueue<T>
	{
		private object lockObject = new object();

		/// <summary>
		/// Queue max size.
		/// </summary>
		public int Limit { get; set; }

		/// <summary>
		/// Adds a new element in the queue.
		/// </summary>
		/// <param name="obj">Element to add.</param>
		public new void Enqueue(T obj)
		{
			base.Enqueue(obj);
			lock (lockObject)
			{
				T overflow;
				while (Count > Limit && TryDequeue(out overflow)) ;
			}
		}
	}
}
