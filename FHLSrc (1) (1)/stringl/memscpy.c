/*===============================================================================================
 * FILE:        memscpy.c
 *
 * DESCRIPTION: Implementation of a secure API memscpy - Size bounded memory copy. 
 *
 *              Copyright (c) 2012 QUALCOMM Technologies Incorporated.
 *              All Rights Reserved. QUALCOMM Proprietary and Confidential.
 *===============================================================================================*/
 
/*===============================================================================================
 *
 *                            Edit History
 *
 *===============================================================================================*/

#include <stringl.h>

size_t  memscpy(
          void        *dst,
          size_t      dst_size,
          const void  *src,
          size_t      src_size
          )
{
  size_t  copy_size = (dst_size <= src_size)? dst_size : src_size;

  memcpy(dst, src, copy_size);

  return copy_size;
}
