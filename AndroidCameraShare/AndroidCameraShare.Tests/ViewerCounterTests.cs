using AndroidCameraShare.Core;

namespace AndroidCameraShare.Tests
{
    public class ViewerCounterTests
    {
        [Fact]
        public void Count_WhenCreated_IsZero()
        {
            ViewerCounter counter = new ViewerCounter();
            Assert.Equal(0, counter.Count);
            Assert.False(counter.HasViewer);
        }
        [Fact]
        public void RegisterSession_WhenFirstViewer_SetsCountToOne()
        {
            ViewerCounter counter = new ViewerCounter();
            counter.RegisterSession();
            Assert.Equal(1, counter.Count);
            Assert.True(counter.HasViewer);
        }
        [Fact]
        public void RegisterSession_WhenSessionReplaced_KeepsCountOne()
        {
            ViewerCounter counter = new ViewerCounter();
            counter.RegisterSession();
            counter.RegisterSession();
            Assert.Equal(1, counter.Count);
            Assert.True(counter.HasViewer);
        }
        [Fact]
        public void Reset_WhenCalled_SetsCountToZero()
        {
            ViewerCounter counter = new ViewerCounter();
            counter.RegisterSession();
            counter.Reset();
            Assert.Equal(0, counter.Count);
            Assert.False(counter.HasViewer);
        }

        [Fact]
        public void Reset_WhenAlreadyZero_DoesNotRaiseChanged()
        {
            ViewerCounter counter = new ViewerCounter();
            int raised = 0;
            counter.Changed += () => raised++;
            counter.Reset();
            Assert.Equal(0, raised);
        }
    }
}
