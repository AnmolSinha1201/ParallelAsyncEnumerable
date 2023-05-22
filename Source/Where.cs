namespace ParallelAsyncEnumerable;

public static partial class Extensions
{
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
		var retVal = await enumerable.Select(item => {
			return Task.Run(async () => {
				await sem.WaitAsync().ConfigureAwait(false);
				var predicatedValue = await predicate(item).ConfigureAwait(false);
				sem.Release();

				return (item, predicatedValue);
			}).ConfigureAwait(false);
		}).ToListAsync().ConfigureAwait(false);

		foreach (var filterableTask in retVal)
		{
			var filterableItem = await filterableTask;
			if (filterableItem.predicatedValue)
				yield return filterableItem.item;
		}
	}
}