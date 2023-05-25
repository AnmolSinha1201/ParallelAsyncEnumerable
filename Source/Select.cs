using System.Threading.Channels;

namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<TOut>> predicate)
	{
		await foreach(var item in enumerable.SelectParallelAsync(ParallelAsyncOptions.Default(), (ct, item) => predicate(item)))
			yield return item;
	}

	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, Func<CancellationToken, TIn, ValueTask<TOut>> predicate)
	{
		await foreach(var item in enumerable.SelectParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, 
	ParallelAsyncOptions options, Func<CancellationToken, TIn, ValueTask<TOut>> predicate)
	{
		var channel = Channel.CreateUnbounded<Task<TOut>>();
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
						try { return await predicate(options.CancellationToken, item); }
						finally { semaphore.Release(); }
					})) // Without Cancellation
					.ConfigureAwait(false);
				}
			}
			finally { channel.Writer.TryComplete(); }
		});

		// Enumeration without cancellation (since we want to finish all enqueued tasks)
		await foreach (var task in channel.Reader.ReadAllAsync().ConfigureAwait(false))
				yield return await task.ConfigureAwait(false);
		await producer.ConfigureAwait(false);
	}
}