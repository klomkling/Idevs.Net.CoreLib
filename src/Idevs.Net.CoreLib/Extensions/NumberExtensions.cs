namespace Idevs.Extensions;

public static class NumberExtensions
{
    public static decimal RoundNumber(this decimal source, short roundMethod = 0, int decimals = 2)
    {
        decimal result = 0;
        if (roundMethod > 0)
        {
            var r = Math.Round(source, 2);
            result = roundMethod switch
            {
                1 => r - Math.Floor(r % 1 * 100) % 25 / 100, // Round down to quarter
                2 => r - Math.Floor(r % 1 * 100) % 50 / 100, // Round down to half
                3 => Math.Floor(r), // Round down to full
                4 => Math.Floor(r) +
                     (Math.Floor(r % 1 * 100) + (25 - Math.Floor(r % 1 * 100) % 25)) / 100, // Round up to quarter
                5 => Math.Floor(r) +
                     (Math.Floor(r % 1 * 100) + (50 - Math.Floor(r % 1 * 100) % 50)) / 100, // Round up to half
                6 => Math.Ceiling(r), // Round up to full
                7 => Math.Floor(r) + Math.Round(Math.Round(r % 1 * 100) / 25, 0) * 25 / 100, // Round to quarter
                8 => Math.Floor(r) + Math.Round(Math.Round(r % 1 * 100) / 50, 0) * 50 / 100, // Round to half
                9 => Math.Round(r, 0), // Round to full
                _ => r // Not round
            };
        }
        else
        {
            result = decimals > 2
                ? Math.Ceiling(source * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals)
                : Math.Round(
                    Math.Round(source * (decimal)Math.Pow(10, decimals + 1)) / (decimal)Math.Pow(10, decimals + 1),
                    decimals);
        }

        return result;
    }

    public static decimal RoundVat(this decimal source, int decimals = 2)
    {
        return Math.Ceiling(source * (decimal)Math.Pow(10, decimals)) / (decimal)Math.Pow(10, decimals);
    }

    /***************************************************
     * Author    : CS Developers
     * Author URI: https://www.comscidev.com
     * Facebook  : https://www.facebook.com/CSDevelopers
     ***************************************************/

    public static string ToThaiBahtText(this string strNumber, bool IsTrillion = false)
    {
        var bahtText = "";
        var strTrillion = "";
        string[] strThaiNumber = { "ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า", "สิบ" };
        string[] strThaiPos = { "", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน", "ล้าน" };

        if (!decimal.TryParse(strNumber, out var decNumber))
        {
            decNumber = 0;
        }

        if (decNumber == 0)
        {
            return "ศูนย์บาทถ้วน";
        }

        strNumber = decNumber.ToString("0.00");
        var strInteger = strNumber.Split('.')[0];
        var strSatang = strNumber.Split('.')[1];

        if (strInteger.Length > 13)
            throw new Exception("รองรับตัวเลขได้เพียง ล้านล้าน เท่านั้น!");

        var isTrillion = strInteger.Length > 7;
        if (isTrillion)
        {
            strTrillion = strInteger[..^6];
            bahtText = ToThaiBahtText(strTrillion, isTrillion);
            strInteger = strInteger[strTrillion.Length..];
        }

        var strLength = strInteger.Length;
        for (var i = 0; i < strInteger.Length; i++)
        {
            var number = strInteger.Substring(i, 1);
            if (number == "0") continue;

            if (i == strLength - 1 && number == "1" && strLength != 1)
            {
                bahtText += "เอ็ด";
            }
            else if (i == strLength - 2 && number == "2" && strLength != 1)
            {
                bahtText += "ยี่";
            }
            else if (i != strLength - 2 || number != "1")
            {
                bahtText += strThaiNumber[int.Parse(number)];
            }

            bahtText += strThaiPos[(strLength - i) - 1];
        }

        if (IsTrillion)
        {
            return bahtText + "ล้าน";
        }

        if (strInteger != "0")
        {
            bahtText += "บาท";
        }

        if (strSatang == "00")
        {
            bahtText += "ถ้วน";
        }
        else
        {
            strLength = strSatang.Length;
            for (var i = 0; i < strSatang.Length; i++)
            {
                var number = strSatang.Substring(i, 1);
                if (number == "0") continue;

                if (i == strLength - 1 && number == "1" && strSatang[0].ToString() != "0")
                {
                    bahtText += "เอ็ด";
                }
                else if (i == strLength - 2 && number == "2" && strSatang[0].ToString() != "0")
                {
                    bahtText += "ยี่";
                }
                else if (i != strLength - 2 || number != "1")
                {
                    bahtText += strThaiNumber[int.Parse(number)];
                }

                bahtText += strThaiPos[(strLength - i) - 1];
            }

            bahtText += "สตางค์";
        }

        return bahtText;
    }
}
