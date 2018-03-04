using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ipotenusa
{
	/// <summary>
	/// Async event handler
	/// </summary>
	public delegate Task AsyncEventHandler<T>(object sender, T data) where T : EventArgs;
}
