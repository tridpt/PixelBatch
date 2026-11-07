# PixelBatch

Ứng dụng Windows 10/11 x64 để nén, đổi kích thước, chuyển định dạng và đóng watermark cho nhiều ảnh. Giao diện tiếng Việt. Xử lý hoàn toàn trên máy; không cần tài khoản hoặc tải ảnh lên máy chủ.

## Dùng ngay

Mở `PixelBatch-Setup-0.6.0.exe` rồi bấm **Cài đặt**. Bấm **Tùy chọn** nếu muốn đổi thư mục cài. Bộ cài tiếng Việt tạo biểu tượng Desktop, Start menu và mục gỡ cài đặt trong Windows Settings → Apps. App cài theo tài khoản Windows hiện tại, mặc định tại `%LOCALAPPDATA%\Programs\PixelBatch`; không cần cài thêm .NET hoặc tải gói phụ khi cài. Có thể mở lại bộ cài để sửa hoặc gỡ app. Gỡ app giữ lại ảnh đã xuất, preset, thiết lập và các tệp tự tạo.

Nếu muốn chạy trực tiếp, mở `dist\PixelBatch.exe`, hoặc giải nén `PixelBatch-Windows-x64-0.6.0.zip` rồi mở `PixelBatch.exe`. Bản này đã kèm .NET và thư viện xử lý ảnh. Khi di chuyển hoặc phân phối app, giữ kèm `THIRD-PARTY-NOTICES.md` và thư mục `Licenses`.

1. Bấm **Thêm ảnh**, **Thêm thư mục**, hoặc kéo ảnh/thư mục vào cửa sổ. Bật **Đọc cả thư mục con** cạnh nút thêm để đọc các nhánh bên trong; mặc định chỉ đọc ảnh nằm trực tiếp trong thư mục. Có thể bấm **Dừng** khi đang tìm hoặc thêm ảnh.
2. Chọn WebP, JPG hoặc PNG; đặt chất lượng và giới hạn chiều rộng/cao hoặc phần trăm. Có thể bật **Nén theo dung lượng mục tiêu** và nhập số KB tối đa cho mỗi ảnh.
3. Bật **Xuất nhiều kích thước cùng lúc** nếu cần nhiều phiên bản. Thêm tối đa 6 cỡ; chọn **Cỡ xem trước** để kiểm tra từng cỡ. Mở **Xoay & lật ảnh** để chỉnh chiều ảnh; có thể chọn **Cắt theo tỉ lệ**, rồi dịch vùng giữ theo ngang/dọc. App mặc định không phóng lớn ảnh nhỏ.
4. Chọn watermark bằng chữ hoặc ảnh logo; chỉnh vị trí, độ hiện và kích thước. Với chữ, bật **Lặp chữ phủ toàn ảnh** để chỉnh góc nghiêng và khoảng cách. Font chữ mặc định là Segoe UI, hỗ trợ tiếng Việt.
5. Chọn ảnh trong danh sách để xem **Kết quả**, **Ảnh gốc** hoặc **So sánh**. Dùng **100%** để xem từng điểm ảnh; cuộn dọc/ngang để dịch vùng xem. Dung lượng kết quả là kích thước của ảnh đã mã hóa với thiết lập hiện tại; **CL** là mức chất lượng thực tế đã dùng.
6. Mở **Tên tệp & gói ZIP** để chọn giữ tên, đặt tên theo mẫu hoặc thay một phần tên cũ. Bật **Giữ cấu trúc thư mục nguồn** nếu muốn giữ từng bộ ảnh; kiểm tra đường dẫn xem trước và tùy chọn ZIP. Chọn thư mục lưu rồi bấm **Xuất ảnh**. **Dừng** giữ các ảnh đã xuất và bỏ phần chưa hoàn tất.
7. Mở **Preset của bạn**, nhập tên rồi bấm **Lưu** để dùng lại thiết lập. Chọn preset đã lưu và bấm **Áp dụng**; lưu cùng tên để cập nhật hoặc bấm **Xóa** để bỏ preset đó.
8. Nếu xuất lỗi, rê chuột vào trạng thái để xem nguyên nhân, sửa thiết lập hoặc thư mục lưu rồi bấm **Thử lại lỗi**. Chỉ các tệp/cỡ bị lỗi được chạy lại.

Phím tắt: `Ctrl+O` thêm ảnh; `Ctrl+Enter` xuất ảnh; `F1` mở hướng dẫn; `Esc` đóng hướng dẫn.

## Mới trong 0.6.0

### Bộ cài Windows

`PixelBatch-Setup-0.6.0.exe` chứa đầy đủ app và thư viện, có chọn thư mục cài, biểu tượng Desktop/Start menu, sửa và gỡ cài đặt. Chạy bộ cài bản mới để nâng cấp; ảnh và thiết lập cá nhân được giữ lại. Bản MSI dành cho cài đặt trực tiếp bằng Windows Installer cũng có trong thư mục dự án. Chỉ cài một trong hai cách; dùng Setup EXE cho luồng thông thường.

### Hướng dẫn và ảnh mẫu

App tự hiện hướng dẫn ba bước khi mở lần đầu, kể cả khi nâng từ bản cũ. Bấm **Dùng ảnh của tôi** để chọn ảnh hoặc **Thử với 3 ảnh mẫu** để thử ngay. Mở lại bằng nút **Hướng dẫn** trên thanh tiêu đề hoặc `F1`. Khi mở app bằng cách truyền đường dẫn ảnh/thư mục, hướng dẫn không che luồng nhập; nó vẫn được cung cấp trong lần mở bình thường tiếp theo nếu chưa xem.

Ảnh mẫu gồm JPG, PNG và WebP, được tạo trên máy tại `%LOCALAPPDATA%\PixelBatch\Samples\v1`, không gọi mạng. Chúng được thêm vào danh sách hiện tại, không xóa ảnh, không đổi thiết lập hoặc thư mục lưu; thêm lại không tạo dòng trùng. Chọn **Cân bằng**, thử **So sánh**, kiểm tra thư mục lưu rồi **Xuất ảnh**. Việc đã xem hướng dẫn được lưu riêng tại `%LOCALAPPDATA%\PixelBatch\getting-started.json`.

## Mới trong 0.5.0

### Đọc cả thư mục con

Bật **Đọc cả thư mục con** trước khi bấm **Thêm thư mục** hoặc kéo/thả thư mục. Tùy chọn áp dụng cho lần thêm tiếp theo; bật nó không tự quét lại danh sách hiện tại. Nếu đã thêm một thư mục khi chưa bật, bật rồi thêm lại cùng thư mục để bổ sung các nhánh còn thiếu. Ảnh đã có trong danh sách không được thêm trùng; một ảnh thuộc về thư mục nguồn của lần thêm đầu tiên.

App tìm ảnh ở luồng nền, giữ giao diện phản hồi và cho phép **Dừng** trong cả lúc tìm tệp và lúc đọc ảnh. Những ảnh đã thêm được giữ lại khi dừng. Giới hạn vẫn là 1.000 tệp cho cả danh sách, kể cả ảnh lỗi; khi đủ sẽ dừng tìm thêm và báo cần xóa bớt. Ảnh hỏng được giữ trong danh sách với trạng thái lỗi; một tệp hoặc nhánh không truy cập được không làm mất các ảnh khác.

Thư mục/tệp liên kết (symbolic link, junction hoặc reparse point) được bỏ qua để tránh vòng lặp. Nếu thư mục lưu kết quả hiện tại nằm bên trong thư mục nguồn, nhánh kết quả đó được bỏ qua khi quét thư mục con để không nhập lại ảnh đã xuất. Thư mục được chọn trực tiếp làm nguồn vẫn có thể được đọc khi trùng với thư mục lưu. Danh sách hiển thị đường dẫn tương đối để phân biệt ảnh cùng tên ở các nhánh khác nhau.

### Giữ cấu trúc thư mục khi xuất

Mở **Tên tệp & gói ZIP** và bật **Giữ cấu trúc thư mục nguồn**. Ví dụ thêm thư mục `Catalog` chứa `Ao\anh.png`: kết quả nằm ở `Catalog\Ao\anh-pixelbatch.png` bên trong thư mục lưu. Khi xuất cỡ 1600 × 1600, đường dẫn là `1600x1600\Catalog\Ao\anh-pixelbatch.png`. Tên tệp vẫn theo thiết lập hậu tố/mẫu/thay chữ; các con số kích thước trong mẫu vẫn là số px thực tế.

Tên thư mục đã thêm được giữ để tách các bộ ảnh. Nếu hai nguồn khác nhau cùng tên `Catalog`, nguồn sau dùng `Catalog (2)`; thêm lại một nguồn đang có trong danh sách sẽ dùng lại tên đã gán. Ảnh thêm riêng qua **Thêm ảnh** hoặc kéo/thả một tệp sẽ nằm trực tiếp trong thư mục kết quả (hoặc thư mục cỡ), vì không có thư mục nguồn được chọn cho tệp đó. Đường dẫn nguồn đã ghi cho mỗi ảnh không đổi khi chuyển chế độ giữ cấu trúc.

Tắt **Giữ cấu trúc thư mục nguồn** để gom kết quả vào một chỗ như trước. Tên ảnh trùng được thêm `(2)`, `(3)`…; ảnh gốc và tệp đã tồn tại không bị ghi đè. Trường xem trước tên ảnh hiển thị cả đường dẫn tương đối khi bật giữ cấu trúc. ZIP giữ đúng các nhánh của ảnh vừa xuất; thử lại lỗi chỉ chạy những tệp/cỡ lỗi và dùng lựa chọn giữ cấu trúc hiện tại.

**Giữ cấu trúc thư mục nguồn** được lưu trong preset và thiết lập gần nhất. **Đọc cả thư mục con** là lựa chọn thêm ảnh riêng trên máy, được nhớ cho lần mở sau. Cả hai mặc định tắt khi đọc thiết lập/preset cũ để giữ hành vi của các phiên bản trước.

## Mới trong 0.4.0

### Xoay và lật ảnh hàng loạt

Xoay phải 90°, 180° hoặc trái 90°; lật ngang và/hoặc lật dọc. Thứ tự xử lý: chuẩn hóa chiều EXIF → xoay thủ công → lật theo chiều ảnh vừa xoay → cắt → đổi kích thước → watermark → nén. Tọa độ vùng cắt được tính trên ảnh sau xoay/lật.

Xem trước, khung so sánh, tên tệp có `{width}`/`{height}` và mọi cỡ xuất đều dùng kích thước sau xoay. Tab **Ảnh gốc** vẫn hiển thị nguồn sau chuẩn hóa EXIF. Thiết lập xoay/lật được lưu trong preset và thiết lập gần nhất.

### Watermark chữ lặp chéo

Trong chế độ chữ, bật **Lặp chữ phủ toàn ảnh** để phủ chữ thành các hàng so le. Góc nghiêng từ -90° đến 90°, mặc định -30°; khoảng cách từ 25% đến 300%, số nhỏ cho chữ dày hơn. Chọn màu trắng/đen, kích thước và độ hiện như watermark thường. Tắt lặp để dùng lại vị trí góc/giữa ảnh. Logo vẫn dùng một vị trí.

Chữ được vẽ sau khi đổi kích thước để mỗi cỡ có mật độ tương ứng. PNG/WebP giữ vùng trong suốt giữa các chữ; độ hiện 0% không làm thay đổi ảnh. Góc và khoảng cách lưu được trong preset cá nhân. Việc vẽ có giới hạn số ô để ảnh rất dài/hẹp vẫn xử lý được.

### Thử lại riêng các tệp xuất lỗi

Nút **Thử lại lỗi (N)** cạnh danh sách ảnh đếm số tệp đầu ra bị lỗi, gồm từng cỡ của một ảnh. Ví dụ một ảnh đã xuất được cỡ 800 nhưng lỗi cỡ 1600: thử lại chỉ chạy cỡ 1600, giữ nguyên tệp 800. Sau khi sửa nguyên nhân, nút thử lại dùng chất lượng, dung lượng mục tiêu, xoay/lật, cắt, watermark, cách đặt tên và thư mục lưu hiện tại.

Các cỡ của lần xuất nhiều kích thước trước được giữ lại khi thử lại, kể cả khi bạn đã sửa danh sách cỡ trên giao diện. Ảnh xuất một cỡ dùng thiết lập kích thước hiện tại. Vị trí đánh số nguồn được giữ từ lần xuất trước, kể cả khi bỏ một dòng ở phía trước; mẫu tên và số bắt đầu vẫn lấy từ thiết lập hiện tại. Nếu cần xuất lại toàn bộ theo danh sách cỡ mới, dùng nút **Xuất ảnh**.

ZIP khi thử lại chỉ chứa những tệp vừa khôi phục trong lần chạy đó, giữ các thư mục cỡ. Có thể dừng và thử lại tiếp; các lỗi chưa khôi phục vẫn được giữ. Tệp chưa chạy do đã dừng và ảnh không đọc được lúc thêm không được tính là lỗi xuất; dùng **Xuất ảnh** để chạy lại cả lô hoặc thêm lại ảnh đã sửa. Thông tin thử lại chỉ giữ trong danh sách đang mở, không lưu qua lần đóng app. Xuất cả lô mới sẽ thay thông tin theo dõi cũ; các tệp đã lưu trên đĩa vẫn được giữ.

## Mới trong 0.3.0

### Cắt theo tỉ lệ và chọn vùng giữ

Hỗ trợ 1:1, 4:5, 16:9, 9:16 và tỉ lệ tùy chỉnh (hai số nguyên từ 1–1.000). Thanh ngang 0% giữ phần trái, 100% giữ phần phải; thanh dọc 0% giữ phần trên, 100% giữ phần dưới. Vị trí chỉ ảnh hưởng tới cạnh cần cắt; ảnh đã cùng tỉ lệ không bị dịch hoặc kéo méo. Vùng cắt được xác định sau khi chuẩn hóa EXIF và xoay/lật thủ công, rồi mới đổi kích thước, thêm watermark và nén.

Tỉ lệ và vị trí cắt áp dụng chung cho cả lô và mọi cỡ xuất. Các cỡ vẫn là khung tối đa. Khi không bật phóng lớn, ảnh nhỏ có thể cho kết quả nhỏ hơn khung yêu cầu. Ví dụ ảnh 2.400 × 1.600 px, cắt 1:1 và xuất cỡ 800 × 800 sẽ cho ảnh vuông 800 × 800; tắt cắt sẽ cho ảnh 800 × 533.

### Đổi tên hàng loạt cho ảnh xuất

Ba chế độ: **Giữ tên gốc + hậu tố**, **Tên theo mẫu / đánh số**, **Thay một phần tên cũ**. Hậu tố chỉ áp dụng chế độ giữ tên. Thay chữ không phân biệt hoa/thường; để trống phần thay bằng để xóa chữ khớp. App không đổi tên hay sửa tệp gốc.

Mẫu tên hỗ trợ:

| Ký hiệu | Nội dung |
| --- | --- |
| `{name}` | Tên ảnh gốc, bỏ phần mở rộng |
| `{sku}` | Mã sản phẩm nhập cho cả lô |
| `{n}` hoặc `{n:000}` | Số thứ tự, có thể đệm từ 1 đến 6 chữ số 0 |
| `{width}` / `{height}` | Số px thực tế của ảnh xuất |

Ví dụ mã `AO`, số bắt đầu `7`, mẫu `{sku}-{n:000}-{width}x{height}` sẽ tạo `AO-007-800x800.jpg`. Số thứ tự theo danh sách ảnh đọc được, giữ cùng số ở mọi cỡ và không đánh lại khi một ảnh xuất bị lỗi. Tên không có ký hiệu số vẫn được phép dùng; nếu trùng tên, app thêm `(2)`, `(3)`…

Tên kết quả từ 1–200 ký tự, không chứa ký tự cấm hoặc tên thiết bị dành riêng của Windows. App kiểm tra tên của toàn bộ lô trước khi xuất. Trường xem trước cho biết tên dự kiến; số chống trùng chỉ được quyết định khi ghi tệp. Tỉ lệ cắt, vị trí và thiết lập tên đều lưu được trong preset cá nhân.

### So sánh trước/sau và zoom 100%

Chọn **So sánh**, kéo thanh phía dưới hoặc kéo trực tiếp trên ảnh. Bên trái là ảnh gốc được xoay/lật, cắt/thu về cùng khung, trước nén và watermark; bên phải là kết quả thực tế. Hai ảnh cùng kích thước và cuộn đồng thời nên có thể so chi tiết trên cùng vị trí.

**Vừa khung** dùng bản xem trước tối đa 1.400 px để giữ giao diện nhẹ. **100%** tải điểm ảnh đầy đủ, ánh xạ một điểm ảnh của ảnh sang một điểm ảnh vật lý trên màn hình, có tính đến DPI của Windows. Tab **Ảnh gốc** ở 100% hiển thị ảnh nguồn đầy đủ sau xoay EXIF. Dùng thanh cuộn ngang/dọc để xem vùng ngoài khung. Với ảnh lớn, tạo bản xem trước 100% cần nhiều bộ nhớ và thời gian hơn chế độ vừa khung.

## Mới trong 0.2.0

### Dung lượng mục tiêu

Nhập từ 1 đến 102.400 KB (1 KB = 1.024 byte). Với JPG/WebP, app thử giảm chất lượng từ mức tối đa trên thanh trượt đến mức tối thiểu bạn chọn. Kích thước, watermark và định dạng được giữ theo thiết lập; app không tự thu nhỏ ảnh thêm để ép đạt dung lượng. Bản xem trước hiển thị dung lượng thực tế, mức chất lượng đã dùng và thông báo đạt/chưa đạt.

Nếu chưa đạt mục tiêu trong khoảng chất lượng đã chọn, ảnh đó không được ghi ra đĩa; các ảnh/cỡ khác vẫn được xử lý. Giảm kích thước, giảm chất lượng tối thiểu hoặc tăng dung lượng mục tiêu rồi thử lại. PNG vẫn nén không mất dữ liệu; mục tiêu chỉ kiểm tra dung lượng. Muốn PNG nhỏ hơn cần giảm kích thước hoặc chuyển sang JPG/WebP.

### Preset cá nhân

Lưu tối đa 50 preset, mỗi tên từ 1–48 ký tự. Preset chứa định dạng, khoảng chất lượng, dung lượng mục tiêu, kích thước/cắt ảnh, watermark, tên tệp và lựa chọn ZIP. Thư mục lưu kết quả được giữ riêng trên máy. Preset dùng logo lưu đường dẫn tới logo; nếu di chuyển logo, hãy chọn lại tệp đó và lưu cập nhật preset.

### Nhiều kích thước

Các cỡ là khung rộng/cao tối đa, không phải lệnh cắt ảnh vuông. Ví dụ ảnh 2.400 × 1.600 px xuất ở cỡ 800 × 800 sẽ thành 800 × 533 px, nằm trong thư mục `800x800`. Mỗi cỡ giữ cùng định dạng, watermark và mục tiêu dung lượng. Khi chọn ZIP, cấu trúc các thư mục cỡ ảnh được giữ nguyên. Trạng thái mỗi ảnh ghi số cỡ đã xuất; rê chuột để xem đường dẫn hoặc lỗi của từng cỡ.

## Hành vi xử lý

- Đọc ảnh JPG/JPEG, PNG, WebP, BMP; xuất JPG, PNG, WebP.
- Tự xoay theo EXIF, chuyển dữ liệu màu về sRGB và loại bỏ metadata khỏi ảnh xuất.
- PNG dùng nén không mất dữ liệu. Chất lượng chỉ áp dụng JPG và WebP; chuyển định dạng không đảm bảo tệp luôn nhỏ hơn ảnh gốc.
- PNG/WebP giữ vùng trong suốt. JPG dùng nền trắng hoặc đen do bạn chọn.
- Giữ nguyên ảnh nguồn. Nếu tên kết quả đã tồn tại, thêm `(2)`, `(3)`... Mỗi tệp hoàn tất được chuyển từ tệp tạm sang tên kết quả; không ghi đè tệp hiện có.
- Lỗi một ảnh không làm dừng các ảnh khác. Rê chuột lên trạng thái lỗi để đọc chi tiết.
- Giới hạn: 1.000 tệp mỗi danh sách; ảnh tối đa 80 megapixel; mỗi cạnh kết quả tối đa 16.000 px. Bản đầu hỗ trợ ảnh tĩnh; chưa xử lý ảnh động, HEIC, RAW hoặc giữ ảnh HDR.
- Thiết lập gần nhất được lưu tại `%LOCALAPPDATA%\PixelBatch\settings.json`; preset cá nhân tại `%LOCALAPPDATA%\PixelBatch\presets.json`. Thiết lập 0.1.0–0.5.0 vẫn đọc được; các tùy chọn xử lý mới mặc định tắt. App không lưu lịch sử nội dung ảnh, không có telemetry và không gọi mạng khi xử lý.

Đây là bản dùng thử Windows 0.6.0. Chưa tích hợp thu tiền, cấp phép Pro hoặc đồng bộ đám mây. App và bộ cài hiện chưa được ký bằng chứng chỉ nhà phát hành; Windows có thể hiển thị thông báo ứng dụng chưa được nhận diện khi tải qua Internet.

## Phát triển và đóng gói

Cần Windows và .NET SDK 8 trở lên. NuGet tải SkiaSharp và các thư viện native khi build lần đầu.

```powershell
dotnet run --project .\PixelBatch.csproj
dotnet run --project .\Checks\PixelBatch.Checks.csproj -c Release
dotnet build .\PixelBatch.csproj -c Release
.\bin\Release\net8.0-windows\PixelBatch.exe --self-test .\qa\ui
.\build.ps1
```

`build.ps1` tạo EXE tự chứa, ZIP, Setup EXE và MSI. Lần build bộ cài đầu tiên tải WiX 3.14.1 từ kho chính thức, kiểm tra SHA-256 rồi giải nén trong `.tools`; không cài công cụ vào Windows. Build cần Windows PowerShell 7/.NET SDK; bộ cài tạo ra không phụ thuộc chúng. Dùng `-SkipInstaller` nếu chỉ cần bản chạy trực tiếp, hoặc `build-installer.ps1` để đóng gói lại từ `dist`. Nguồn bộ cài nằm trong `Installer`.

`--self-test` mở cửa sổ ngoài vùng hiển thị, tạo dữ liệu kiểm tra trong thư mục chỉ định, chạy hướng dẫn/ảnh mẫu và luồng nhập–xoay/lật–cắt–watermark lặp–đặt tên–so sánh/100%–preset–xuất nhiều cỡ–dung lượng mục tiêu–ZIP–hủy–thử lại lỗi–thư mục con/giữ cấu trúc rồi chụp giao diện. Preset và trạng thái hướng dẫn kiểm tra nằm trong thư mục QA riêng. Không thay đổi clipboard hoặc dữ liệu ảnh của người dùng. Có thể chạy lệnh này với EXE đã đóng gói; dùng một thư mục đầu ra QA mới cho mỗi lần chạy.

Kiểm tra bộ cài bằng `.\Installer\Test-Installer.ps1`: script tạo hai bản cài có tên/GUID QA riêng; đặt biểu tượng thử bên trong thư mục QA, rồi kiểm tra cài ở đường dẫn tiếng Việt, nâng cấp, sửa, chạy toàn bộ UI checks trên EXE đã cài và gỡ app. Script giữ lại báo cáo, nhật ký và một tệp mẫu do người dùng tự tạo để xác minh thao tác gỡ không xóa nhầm. Không cài đè bản PixelBatch thật.

## Công nghệ

C# / WPF / .NET 8, SkiaSharp. Giao diện Windows native; không cần trình duyệt hoặc Electron. Bản Web/Android sẽ cần giao diện và lớp tích hợp riêng.
