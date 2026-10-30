export function validateImageUpload(file) {
 if(!file || !['image/jpeg','image/png','image/webp'].includes(file.type))return 'Choose a JPG, PNG or WebP image.';
 if(file.size<=0 || file.size>10*1024*1024)return 'Choose a non-empty image up to 10 MB.';
 return '';
}
export function fitImageRect(width,height,size=512){
 if(width<=0||height<=0)throw new Error('The image dimensions are invalid.');
 const ratio=Math.min(size/width,size/height);return {x:(size-width*ratio)/2,y:(size-height*ratio)/2,width:width*ratio,height:height*ratio};
}
