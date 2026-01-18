/*===============================================================================================
 * FILE:        memsmove.c
 *
 * DESCRIPTION: Implementation of a secure API memsmove - Size bounded memory move.
 *
 *              Copyright (c) 2012 QUALCOMM Technologies Incorporated.
 *              All Rights Reserved. QUALCOMM Proprietary and Confidential.
 *===============================================================================================*/
 
/*===============================================================================================
 *
 *                            Edit History
 *
 *===============================================================================================*/
 
#include "stringl.h"

size_t memsmove(
          void        *dst,
          size_t      dst_size,
          const void  *src,
          size_t      src_size
          )
{
  size_t  copy_size = (dst_size <= src_size)? dst_size : src_size;

  memmove(dst, src, copy_size);

  return copy_size;
}

