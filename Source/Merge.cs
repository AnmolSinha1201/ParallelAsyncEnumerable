using System.Threading.Channels;

namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	private static Task<bool> FinishedTask = new TaskCompletionSource<bool>().Task;

	// Derived from https://stackoverflow.com/questions/66152698/how-to-query-two-iasyncenumerables-asynchronously
	// and https://github.com/dotnet/reactive/blob/main/Ix.NET/Source/System.Interactive.Async/System/Linq/Operators/Merge.cs
	public static async IAsyncEnumerable<TSource> Merge<TSource>(
	this IEnumerable<IAsyncEnumerable<TSource>> sources, ParallelAsyncOptions options)
	{
		var count = sources.Count();

		var enumerators = new IAsyncEnumerator<TSource>[count];
		var moveNextTasks = new Task<bool>[count];
		var channel = Channel.CreateUnbounded<TSource>();

		var producer = Task.Run(async () => 
		{
			try
			{
				for (var i = 0; i < count; i++)
				{
					var _enumerator = sources.ElementAt(i).GetAsyncEnumerator(options.CancellationToken);
					enumerators[i] = _enumerator;
					moveNextTasks[i] = _enumerator.MoveNextAsync().AsTask();
				}

				// moveNextTasks.All(i => i.IsCompleted) doesn't work because FinishedTask isn't truly a finished task.
				// This can be solved by using a list to keep track of finished tasks but that would mean also keeping track
				// of removed indexes
				var active = count;
				while (active > 0)
				{
					var moveNextTask = await Task.WhenAny(moveNextTasks).ConfigureAwait(false);
					var index = Array.IndexOf(moveNextTasks, moveNextTask);
					var enumerator = enumerators[index];

					// awaiting MoveNextAsync. If it returns false, it means we have reached end of the enumeration.
					if (await moveNextTask.ConfigureAwait(false))
					{
						var item = enumerator.Current;
						moveNextTasks[index] = enumerator.MoveNextAsync().AsTask();
						await channel.Writer.WriteAsync(item);
					}
					else
					{
						moveNextTasks[index] = FinishedTask;
						await enumerator.DisposeAsync().ConfigureAwait(false);
						active--;
					}
				}
			}
			catch
			{
				var errors = new List<Exception>();

				for (var i = 0; i < count; i++)
				{
					var moveNextTask = moveNextTasks[i];
					var enumerator = enumerators[i];

					if (moveNextTask.Exception != null)
						errors.Add(moveNextTask.Exception);
					
					if (enumerator != null)
						await enumerator.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally{ channel.Writer.TryComplete(); }
		});

		await foreach (var item in channel.Reader.ReadAllAsync().ConfigureAwait(false))
				yield return item;
		await producer.ConfigureAwait(false);
	}
}