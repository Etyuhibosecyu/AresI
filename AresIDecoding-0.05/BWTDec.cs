
namespace AresILib;

public class BWTDec(NList<ShortIntervalList> input, uint rAlpha, int delta, bool enoughTransparency, int bwtBlockSize)
{
	protected ShortIntervalList list = [];
	protected byte newSerie, newSerieQ, deltaSum;
	protected int leftSerie;

	public virtual NList<ShortIntervalList> Decode(NList<byte> skipped)
	{
		var bwtBlockExtraSize = bwtBlockSize <= 0x4000 ? 2 : bwtBlockSize <= 0x400000 ? 3 : bwtBlockSize <= 0x40000000 ? 4 : 5;
		Status[0] = 0;
		StatusMaximum[0] = GetArrayLength(input.Length, bwtBlockSize + bwtBlockExtraSize);
		var numericInput = input.Convert(x => (short)x[0].Lower);
		NList<byte> bytes2 = [];
		for (var i = 0; i < numericInput.Length;)
		{
			int zle = numericInput[i] & ValuesInByte >> 1, rle = numericInput[i] & ValuesInByte >> 2;
			bytes2.AddRange(numericInput.GetSlice(i..(i += bwtBlockExtraSize)).Convert(x => (byte)x));
			bytes2.AddRange(rle != 0 ? DecodeRLEAfterBWT(numericInput.Convert(x => (byte)x), ref i) : zle != 0 ? DecodeZLE(numericInput, ref i) : numericInput.GetRange(i..Min(i += bwtBlockSize, numericInput.Length)).Convert(x => (byte)x));
		}
		var hs = bytes2.Filter((x, index) => index % (bwtBlockSize + bwtBlockExtraSize) >= bwtBlockExtraSize).ToHashSet().Concat(skipped).Sort().ToHashSet();
		NList<ShortIntervalList> result = new(bytes2.Length);
		list.Clear();
		leftSerie = newSerie = newSerieQ = deltaSum = 0;
		for (var i = 0; i < bytes2.Length; i += bwtBlockSize, Status[0]++)
		{
			if (bytes2.Length - i <= bwtBlockExtraSize)
				throw new DecoderFallbackException();
			var length = Min(bwtBlockSize, bytes2.Length - i - bwtBlockExtraSize);
			bytes2[i] &= (ValuesInByte >> 2) - 1;
			var firstPermutation = (int)bytes2.GetSlice(i, bwtBlockExtraSize).Progression(0L, (x, y) => unchecked((x << BitsPerByte) + y));
			i += bwtBlockExtraSize;
			result.AddRange(DecodeBWT2(bytes2.GetRange(i, length), hs, firstPermutation));
		}
		result.Add(list);
		return result;
	}

	protected virtual NList<ShortIntervalList> DecodeBWT2(NList<byte> input, ListHashSet<byte> hs, int firstPermutation)
	{
		var mtfMemory = hs.ToArray();
		for (var i = 0; i < input.Length; i++)
		{
			var index = hs.IndexOf(input[i]);
			input[i] = mtfMemory[index];
			Array.Copy(mtfMemory, 0, mtfMemory, 1, index);
			mtfMemory[0] = input[i];
		}
		var sorted = input.ToArray((elem, index) => (elem, index)).NSort(x => x.elem);
		var convert = sorted.ToArray(x => x.index);
		List<int> values = [];
		var result = new NList<ShortIntervalList>(input.Length / 3);
		var it = firstPermutation;
		for (var i = 0; i < input.Length;)
		{
			if (list.Length == 0)
			{
				if (rAlpha == 0)
					list.Add(Interval.Default);
				else
				{
					list.Add(new(input[it = convert[it]], ValuesInByte));
					deltaSum = delta == 0 || i == 0 ? input[it] : unchecked((byte)(deltaSum + input[it] + (ValuesInByte >> 1)));
					i++;
				}
			}
			if (i > input.Length)
				throw new DecoderFallbackException();
			else if (i == input.Length)
				break;
			if (rAlpha == 0 || deltaSum != 0 || !enoughTransparency)
			{
				if (list.Length < 2)
				{
					list.Add(new(input[it = convert[it]], ValuesInByte));
					i++;
				}
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
				if (list.Length < 3)
				{
					list.Add(new(input[it = convert[it]], ValuesInByte));
					i++;
				}
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
				if (list.Length < 4)
				{
					list.Add(new(input[it = convert[it]], ValuesInByte));
					i++;
				}
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
			}
			else
			{
				list.Add(Interval.Default);
				list.Add(Interval.Default);
				list.Add(Interval.Default);
			}
			if (leftSerie > 0)
				leftSerie--;
			else
			{
				if (list.Length < 5)
				{
					newSerie = input[it = convert[it]];
					list.Add(new(newSerie, ValuesInByte));
					i++;
				}
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
				int value;
				if (newSerie % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				{
					value = newSerie % (ValuesInByte >> 1) + 1;
					goto shortRLE;
				}
				if (list.Length < 6)
				{
					newSerieQ = input[it = convert[it]];
					list.Add(new(newSerieQ, ValuesInByte));
					i++;
				}
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
				var newSerieR = input[it = convert[it]];
				list.Add(new(newSerieR, ValuesInByte));
				i++;
				value = (newSerieQ << BitsPerByte) + newSerieR + (ValuesInByte >> 1);
				if (i > input.Length)
					throw new DecoderFallbackException();
				else if (i == input.Length)
					break;
				shortRLE:
				if (newSerie >= ValuesInByte >> 1)
					leftSerie = value - 2;
				else if (delta != 0)
					deltaSum += unchecked((byte)((value - 1) * (list[0].Lower + (ValuesInByte >> 1))));
				if (i > input.Length)
					throw new DecoderFallbackException();
			}
			result.Add(new(list));
			list.Clear();
		}
		return result;
	}

	public NList<byte> DecodeRLEAfterBWT(Slice<byte> byteList, ref int i)
	{
		if (i >= byteList.Length)
			throw new DecoderFallbackException();
		var zero = byteList[i++];
		NList<byte> result = [];
		int length, serie, l;
		byte temp;
		for (; i < byteList.Length && result.Length < bwtBlockSize;)
		{
			result.Add(byteList[i++]);
			if (i >= byteList.Length || result.Length >= bwtBlockSize)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				serie = 2;
			else
				serie = 1;
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1 || result.Length >= bwtBlockSize - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (result.Length + length > bwtBlockSize)
				throw new DecoderFallbackException();
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(zero);
				continue;
			}
			l = Min(length, byteList.Length - i);
			result.AddRange(byteList.GetRange(i, l));
			i += l;
			if (l >= ValuesIn2Bytes)
				continue;
			if (i >= byteList.Length || result.Length >= bwtBlockSize)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				throw new DecoderFallbackException();
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1 || result.Length >= bwtBlockSize - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (result.Length + length > bwtBlockSize)
				throw new DecoderFallbackException();
			for (var j = 0; j < length; j++)
				result.Add(zero);
		}
		return result;
	}

	public virtual NList<byte> DecodeZLE(Slice<short> byteList, ref int i)
	{
		if (i >= byteList.Length)
			throw new DecoderFallbackException();
		short zero = byteList[i++], zeroB = byteList[i++];
		NList<byte> result = new(bwtBlockSize);
		String zeroCode = ['1'];
		int length;
		for (; i < byteList.Length && result.Length < bwtBlockSize;)
		{
			while (i < byteList.Length && result.Length < bwtBlockSize && byteList[i] != zero && byteList[i] != zeroB)
				result.Add((byte)byteList[i++]);
			if (i >= byteList.Length || result.Length >= bwtBlockSize)
				break;
			zeroCode.Remove(1);
			length = 0;
			while (i < byteList.Length && result.Length + length < bwtBlockSize && (byteList[i] == zero || byteList[i] == zeroB))
			{
				zeroCode.Add(byteList[i++] == zero ? '0' : '1');
				length = (int)(new MpzT(zeroCode.ToString(), 2) - 1);
			}
			using var streamOfZeros = RedStarLinq.NFill((byte)zero, length);
			result.AddRange(streamOfZeros);
		}
		return result;
	}
}
