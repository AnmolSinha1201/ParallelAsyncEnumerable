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
		var retVal = await enumerable.Select(item => {

			return Task.Run(async () => {
				await sem.WaitAsync().ConfigureAwait(false);
				var retVal = await predicate(item).ConfigureAwait(false);
				sem.Release();

				return retVal;
			}, options.Token).ConfigureAwait(false);
		}).ToListAsync(options.Token).ConfigureAwait(false);

		foreach (var item in retVal)
			yield return await item;	
	}
}