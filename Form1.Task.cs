using System.Threading.Tasks;
using UBCS2_A.Helpers;
using UBCS2_A.Services;

namespace UBCS2_A
{
    public partial class Form1
    {
        private TaskContext _taskContext;
        private TaskManager _taskManager;

        private void SetupTaskSystem(FirebaseService firebase)
        {
            _taskContext = new TaskContext(firebase);

            if (dgvTask != null && txtTaskSID != null)
            {
                _taskContext.RegisterTaskTable(dgvTask, "T_Tasks", 100);
                _taskManager = new TaskManager(txtTaskSID, _taskContext);
            }
        }

        private async Task StartTaskSyncAsync()
        {
            if (_taskContext != null)
            {
                _taskContext.StartSync();
                await _taskContext.LoadInitialDataAsync();
            }
        }
    }
}