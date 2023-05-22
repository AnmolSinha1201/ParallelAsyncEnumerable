namespace ParallelAsyncEnumerable;

public class ParallelAsyncOptions
{
	public int MaxDegreeOfParallelism;
	public CancellationToken Token = default;

	public static ParallelAsyncOptions Default()
	{
		var retVal = new ParallelAsyncOptions();
		retVal.MaxDegreeOfParallelism = Environment.ProcessorCount * 2;

		return retVal;
	}
}