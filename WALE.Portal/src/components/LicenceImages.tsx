import {useEffect, useState} from "react";
import {waleApiClient, waleApiBaseUrl} from "../api/apiClient";
import {PageImage} from "../api/generated/apiClient";

interface LicenceImagesProps {
    filename: string;
}

export function LicenceImages({filename}: LicenceImagesProps) {
    const [images, setImages] = useState<PageImage[]>([]);

    useEffect(() => {
        waleApiClient.pageImages(filename, undefined)
            .then(data => {
                const sortedImages = data.sort((a, b) => {
                    if (a.pageNumber !== b.pageNumber) {
                        return a.pageNumber - b.pageNumber;
                    }
                    return a.imageNumber - b.imageNumber;
                });
                setImages(sortedImages);
            });
    }, [filename]);

    return (
        <div className="licence-images">
            {images.map((image, index) => {
                const imageUrl = `${waleApiBaseUrl}/BFF/Images/PartialPageImage?filename=${encodeURIComponent(filename)}&extension=${encodeURIComponent(image.extension)}&pageNumber=${image.pageNumber}&imageNumber=${image.imageNumber}`;
                return (
                    <div key={index} className="licence-image-container" style={{ marginBottom: '20px' }}>
                        <img 
                            src={imageUrl} 
                            alt={`Page ${image.pageNumber} Image ${image.imageNumber}`}
                            style={{ maxWidth: '100%', height: 'auto', display: 'block' }}
                        />
                        <p className="image-caption">
                            Page: {image.pageNumber}, Image: {image.imageNumber}, Width: {image.width}px, Height: {image.height}px
                        </p>
                    </div>
                );
            })}
        </div>
    );
}

export default LicenceImages;
