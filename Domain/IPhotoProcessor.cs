using Domain.Models;
using OpenCvSharp;

namespace Domain;

public interface IPhotoProcessor
{
	GreekSymbolImage Process(FileStream stream);

	GreekSymbolImage Process(Mat mat);
}