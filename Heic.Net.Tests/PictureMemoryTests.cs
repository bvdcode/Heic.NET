using Heic.Net.Hevc;
using Xunit;

namespace Heic.Net.Tests
{
    public class PictureMemoryTests
    {
        [Fact]
        public void PlanePublishesOnlyCompleteReconstructedBlocks()
        {
            SamplePlane plane = new SamplePlane(8, 8);
            int[] prediction = new int[16];
            int[] residual = new int[16];
            prediction[0] = 255;
            residual[0] = 100;
            residual[1] = -100;
            Assert.Equal(-1, plane.Neighbour(0, 0));
            plane.Store(0, 0, 4, prediction, residual, 8);
            Assert.Equal(255, plane.Neighbour(0, 0));
            Assert.Equal(0, plane.Neighbour(1, 0));
            Assert.Equal(0, plane.Neighbour(3, 3));
            Assert.Equal(-1, plane.Neighbour(4, 0));
            Assert.Equal(-1, plane.Neighbour(0, 4));
            Assert.Equal(-1, plane.Neighbour(-1, 0));
            prediction[0] = 0;
            residual[0] = 0;
            Assert.Equal(255, plane.Neighbour(0, 0));
        }

        [Fact]
        public void BlockMapsKeepPredictionAndTransformCoordinatesIndependent()
        {
            CodingBlockMap blocks = new CodingBlockMap(16, 16);
            blocks.SetCodingUnit(0, 0, 16, 0, false);
            blocks.SetMode(0, 0, 8, 26);
            blocks.SetMode(8, 0, 8, 10);
            blocks.SetQp(0, 0, 16, 16, 27);
            blocks.MarkTransform(4, 4, 4);
            Assert.Equal(26, blocks.Mode(7, 7));
            Assert.Equal(10, blocks.Mode(8, 7));
            Assert.Equal(1, blocks.Mode(0, -1));
            Assert.Equal(27, blocks.Qp(15, 15));
            Assert.True(blocks.Boundary(4, 4, true));
            Assert.True(blocks.Boundary(4, 4, false));
            Assert.False(blocks.Boundary(8, 8, true));
        }
    }
}
