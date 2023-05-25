using System.Threading.Channels;

namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<bool>> predicate)
	{
		await foreach (var item in enumerable.WhereParallelAsync(ParallelAsyncOptions.Default(), (ct, item) => predicate(item)))
			yield return item;
	}

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, Func<CancellationToken, TIn, ValueTask<bool>> predicate)
	{
		await foreach (var item in enumerable.WhereParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, 
	ParallelAsyncOptions options, Func<CancellationToken, TIn, ValueTask<bool>> predicate)
	{
		var channel = Channel.CreateUnbounded<Task<(TIn Item, bool PredicatedBool)>>();
		using SemaphoreSlim semaphore = new(options.MaxDegreeOfParallelism, options.MaxDegreeOfParallelism);
		
		Task producer = Task.Run(async () =>
		{
			try
			{
				await foreach (var item in enumerable.WithCancellation(options.CancellationToken).ConfigureAwait(false))
				{
					await semaphore.WaitAsync(options.CancellationToken).ConfigureAwait(false);
					await channel.Writer.WriteAsync(Task.Run(async () => 
					{
						try 
						{ 
							var predicatedBool =  await predicate(options.CancellationToken, item); 
							return (item, predicatedBool);
						}
						finally { semaphore.Release(); }
					})) // Without Cancellation
					.ConfigureAwait(false);
				}
			}
			finally { channel.Writer.TryComplete(); }
		});

		// Enumeration without cancellation (since we want to finish all enqueued tasks)
		await foreach (var task in channel.Reader.ReadAllAsync().ConfigureAwait(false))
		{
			var result = await task.ConfigureAwait(false);
			if (result.PredicatedBool)
				yield return result.Item;
		}
				
		await producer.ConfigureAwait(false);
	}
}