namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
	/*
	NOTE : About launching tasks from the enumerable (refer to General Notes)
	NOTE : About SynchronizationContext (ConfigureAwait) (refer to General Notes)
	*/

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, Func<TIn, ValueTask<bool>> predicate)
	{
		await foreach (var item in enumerable.WhereParallelAsync(ParallelAsyncOptions.Default(), predicate))
			yield return item;
	}

	public static async IAsyncEnumerable<TIn> WhereParallelAsync<TIn>(
	this IAsyncEnumerable<TIn> enumerable, ParallelAsyncOptions options, Func<TIn, ValueTask<bool>> predicate)
	{
		var sem = new SemaphoreSlim(options.MaxDegreeOfParallelism, options.MaxDegreeOfParallelism);
		var retVal = await enumerable.Select(async item => {
			await sem.WaitAsync().ConfigureAwait(false);

			return Task.Run(async () => {
				var predicatedBool = await predicate(item).ConfigureAwait(false);
				sem.Release();

				return (item, predicatedBool);
			}).ConfigureAwait(false);
		}).ToListAsync().ConfigureAwait(false);

		foreach (var filterableTask in retVal)
		{
			var filterableItem = await (await filterableTask);
			if (filterableItem.predicatedBool)
				yield return filterableItem.item;
		}
	}
}