namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	/*
	NOTE : About launching tasks from the enumerable (refer to General Notes)
	NOTE : About SynchronizationContext (ConfigureAwait) (refer to General Notes)
	*/

	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<TOut>> predicate)
	{
		await foreach(var item in enumerable.SelectParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TOut> SelectParallelAsync<TIn, TOut>(
	this IAsyncEnumerable<TIn> enumerable, ParallelAsyncOptions options, Func<TIn, ValueTask<TOut>> predicate)
	{
		var sem = new SemaphoreSlim(options.MaxDegreeOfParallelism, options.MaxDegreeOfParallelism);
		var retVal = await enumerable.Select(async item => {
			await sem.WaitAsync(options.CancellationToken).ConfigureAwait(false);

			return Task.Run(async () => {
				try
				{
					var retVal = await predicate(item).ConfigureAwait(false);
					return retVal;
				}
				catch { throw; }
				finally { sem.Release(); }
				
			}, options.CancellationToken).ConfigureAwait(false);
		})
		.ToListAsync(options.CancellationToken)
		.ConfigureAwait(false);

		foreach (var item in retVal)
			yield return await (await item);	
	}
}