import pathlib
p = pathlib.Path(r"q:/said/FactoryApp/SupplierWindow.xaml")
t = p.read_text(encoding="utf-8")
t = t.replace("Content=\"\u0090 ????\"", "Content=\"\u2190 ????\"")
# common mojibake for left arrow + space + arabic
import re
# fix back button if still wrong pattern
lines = []
for line in t.splitlines(True):
    if "BackButton" in line and "????" in line and "\u2190" not in line:
        line = re.sub(r'Content="[^"]*????"', 'Content="\u2190 ????"', line)
    lines.append(line)
t = "".join(lines)
if "?? ???? ???? ?????" not in t:
    t = re.sub(
        r'(<TextBlock x:Name="ReceiptPreviewPlaceholderText" Text=")[^"]*(")',
        r'\1?? ???? ???? ?????\2',
        t,
        count=1,
    )
p.write_text(t, encoding="utf-8")
print("written")
