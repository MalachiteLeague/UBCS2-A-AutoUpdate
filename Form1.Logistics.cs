using System.Threading.Tasks;
using Microsoft.Office.Interop.Excel;
using UBCS2_A.Helpers;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1
    {
        private LogisticsContext _logisticsContext;
        private MatrixManager _matrixManager;
        private InputGroupManager _inputGroupManager;

        private void SetupLogisticsSystem(FirebaseService firebase)
        {
            _logisticsContext = new LogisticsContext(firebase);

            if (dgvGiaoNhan != null)
            {
                // 1. Setup Matrix
                _matrixManager = new MatrixManager(dgvGiaoNhan);
                _logisticsContext.RegisterMatrixTable(_matrixManager, "T_Logistics_Matrix");

                // 2. Setup Input Group (Nhập liệu)
                if (dgvInput != null && btnGui != null)
                {
                    _inputGroupManager = new InputGroupManager(
                        _matrixManager,
                        _firebaseService, // [THÊM DÒNG NÀY] Truyền service vào
                        dgvInput,
                        btnGui,
                        cboNguoiGui,
                        cboNguoiNhan,
                        txtCarrier,
                        cboLine,
                        radKhac, radDen, radDo, radXanhLa, radXanhDuong, radNuocTieu, radPCD
                    );
                }
            }
        }

        private async Task StartLogisticsSyncAsync()
        {
            if (_logisticsContext != null)
            {
                _logisticsContext.PrepareSync();
                await _logisticsContext.LoadInitialDataAsync();
            }
        }
    }
}