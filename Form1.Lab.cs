using System.Threading.Tasks;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1
    {
        private LabDataContext _context;

        private void SetupLabSystem(FirebaseService firebase)
        {
            _context = new LabDataContext(firebase);

            // Tầng 1
            if (DataCTMT1 != null) _context.RegisterTable(DataCTMT1, "T1_HuyetHoc_CongThucMau");
            if (DataDMT1 != null) _context.RegisterTable(DataDMT1, "T1_HuyetHoc_DongMau");
            if (DataGST1 != null) _context.RegisterTable(DataGST1, "T1_HuyetHoc_GiamSat");
            if (DataSHT1 != null) _context.RegisterTable(DataSHT1, "T1_SinhHoa");
            if (DataMDT1 != null) _context.RegisterTable(DataMDT1, "T1_MienDich");

            // Tầng 3
            if (DataCTMT3 != null) _context.RegisterTable(DataCTMT3, "T3_HuyetHoc_CongThucMau");
            if (DataDMT3 != null) _context.RegisterTable(DataDMT3, "T3_HuyetHoc_DongMau");
            if (DataGST3 != null) _context.RegisterTable(DataGST3, "T3_HuyetHoc_GiamSat");
            if (DataSHMDT3 != null) _context.RegisterTable(DataSHMDT3, "T3_SinhHoa_MienDich");
        }

        private async Task StartLabSyncAsync()
        {
            if (_context != null) await _context.StartAllAsync();
        }
    }
}